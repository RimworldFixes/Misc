using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using HugsLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using HarmonyLib;
using System.Text;
using UnityEngine;
using HugsLib.Utils;
using Verse.AI.Group;
using HugsLib.Settings;
using RimWorld.QuestGen;

namespace SaveOurShip2
{
    [StaticConstructorOnStartup]
    public class ShipInteriorMod2 : ModBase
    {
        public static List<Building> closedSet = new List<Building>();

        public static List<Building> openSet = new List<Building>();

        public static HugsLib.Utils.ModLogger instLogger;

        public static bool saveShip;
        public static bool AirlockBugFlag = false;

        public static Building shipRoot;

        public static float crittersleepBodySize = 0.7f;

        public static Graphic shipZero = GraphicDatabase.Get(typeof(Graphic_Single), "UI/Ship_Icon_Off", ShaderDatabase.Cutout, new Vector2(1, 1), Color.white, Color.white);
        public static Graphic shipOne = GraphicDatabase.Get(typeof(Graphic_Single), "UI/Ship_Icon_On_slow", ShaderDatabase.Cutout, new Vector2(1, 1), Color.white, Color.white);
        public static Graphic shipTwo = GraphicDatabase.Get(typeof(Graphic_Single), "UI/Ship_Icon_On_mid", ShaderDatabase.Cutout, new Vector2(1, 1), Color.white, Color.white);
        public static Graphic shipThree = GraphicDatabase.Get(typeof(Graphic_Single), "UI/Ship_Icon_On_fast", ShaderDatabase.Cutout, new Vector2(1, 1), Color.white, Color.white);
        public static Graphic ruler = GraphicDatabase.Get(typeof(Graphic_Single), "UI/ShipRangeRuler", ShaderDatabase.Cutout, new Vector2(1, 1), Color.white, Color.white);
        public static Graphic projectile = GraphicDatabase.Get(typeof(Graphic_Single), "UI/ShipProjectile", ShaderDatabase.Cutout, new Vector2(1, 1), Color.white, Color.white);
        public static Graphic shipBar = GraphicDatabase.Get(typeof(Graphic_Single), "UI/Enemy Ship Icon", ShaderDatabase.Cutout, new Vector2(1, 1), Color.white, Color.white);

        public static Texture2D PowerTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.45f, 0.425f, 0.1f));
        public static Texture2D HeatTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.5f, 0.1f, 0.1f));

        public static Backstory hologramBackstory;

        public static BiomeDef OuterSpaceBiome = DefDatabase<BiomeDef>.GetNamed("OuterSpaceBiome");

        public static HediffDef hypoxia = HediffDef.Named("SpaceHypoxia");

        static ShipInteriorMod2()
        {
            /*hologramBackstory = new Backstory();
            hologramBackstory.identifier = "SoSHologram";
            hologramBackstory.slot = BackstorySlot.Childhood;
            hologramBackstory.title = "machine persona";
            hologramBackstory.titleFemale = "machine persona";
            hologramBackstory.titleShort = "persona";
            hologramBackstory.titleShortFemale = "persona";
            hologramBackstory.baseDesc = "{PAWN_nameDef} is a machine persona. {PAWN_pronoun} interacts with the world via a hologram, which cannot leave the map where {PAWN_possessive} core resides.";
            hologramBackstory.shuffleable = false;
            typeof(Backstory).GetField("bodyTypeFemaleResolved", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hologramBackstory, BodyTypeDefOf.Female);
            typeof(Backstory).GetField("bodyTypeMaleResolved", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hologramBackstory, BodyTypeDefOf.Male);
            hologramBackstory.spawnCategories = new List<string>();
            hologramBackstory.spawnCategories.Add("SoSHologram");*/
        }

        public override string ModIdentifier {
            get
            {
                return "ShipInteriorMod2";
            }
        }

        public ShipInteriorMod2()
        {
        }

        public static SettingHandle<int> minTravelTime;
        public static SettingHandle<int> maxTravelTime;
        public static SettingHandle<bool> renderPlanet;

        public override void DefsLoaded()
        {
            base.DefsLoaded();
            minTravelTime = Settings.GetHandle("minTravelTime", "Minimum Travel Time", "Minimum number of years that pass when traveling to a new world.", 5);
            maxTravelTime = Settings.GetHandle("maxTravelTime", "Maximum Travel Time", "Maximum number of years that pass when traveling to a new world.", 100);
            renderPlanet = Settings.GetHandle("renderPlanet", "Dynamic Planet Rendering", "If checked, orbital maps will show a day/night cycle on the planet. Disable this option if the game runs slowly in space.", false);
            /*foreach (TraitDef AITrait in DefDatabase<TraitDef>.AllDefs.Where(t => t.exclusionTags.Contains("AITrait")))
            {
                typeof(TraitDef).GetField("commonality", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(AITrait, 0);
            }*/
        }

        public static bool hasSpaceSuit(Pawn thePawn)
        {
            bool hasHelmet = false;
            bool hasSuit = false;
            if (thePawn.def.tradeTags != null && thePawn.def.tradeTags.Contains("AnimalInsectSpace"))
                return true;
            if (thePawn.RaceProps.IsMechanoid)
                return true;
            if (thePawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("SpaceBeltBubbleHediff")) != null)
                return true;
            if (thePawn.apparel == null)
                return false;
            bool hasBelt = false;
            Apparel belt = null;
            foreach (Apparel app in thePawn.apparel.WornApparel)
            {
                if (app.def.apparel.tags.Contains("EVA"))
                {
                    if (app.def.apparel.layers.Contains(ApparelLayerDefOf.Overhead))
                        hasHelmet = true;
                    if (app.def.apparel.layers.Contains(ApparelLayerDefOf.Shell))
                        hasSuit = true;
                }
                else if (app.def.defName.Equals("Apparel_SpaceSurvivalBelt"))
                {
                    hasBelt = true;
                    belt = app;
                }

            }
            if (hasHelmet && hasSuit)
                return true;
            if (hasBelt)
            {
                thePawn.health.AddHediff(HediffDef.Named("SpaceBeltBubbleHediff"));
                thePawn.apparel.Remove(belt);
                thePawn.apparel.Wear((Apparel)ThingMaker.MakeThing(ThingDef.Named("Apparel_SpaceSurvivalBeltDummy")), false, true);
                GenExplosion.DoExplosion(thePawn.Position, thePawn.Map, 1, DamageDefOf.Smoke, null, -1, -1f, null, null, null, null, null, 1f);
                return true;
            }
            return false;
        }

        public static void RectangleUtility(int xCorner, int zCorner, int x, int z, ref List<IntVec3> border, ref List<IntVec3> interior)
        {
            for (int ecks = xCorner; ecks < xCorner + x; ecks++)
            {
                for (int zee = zCorner; zee < zCorner + z; zee++)
                {
                    if (ecks == xCorner || ecks == xCorner + x - 1 || zee == zCorner || zee == zCorner + z - 1)
                        border.Add(new IntVec3(ecks, 0, zee));
                    else
                        interior.Add(new IntVec3(ecks, 0, zee));
                }
            }
        }

        public static void CircleUtility(int xCenter, int zCenter, int radius, ref List<IntVec3> border, ref List<IntVec3> interior)
        {
            border = CircleBorder(xCenter, zCenter, radius);
            int reducedRadius = radius - 1;
            while (reducedRadius > 0)
            {
                List<IntVec3> newCircle = CircleBorder(xCenter, zCenter, reducedRadius);
                foreach (IntVec3 vec in newCircle)
                    interior.Add(vec);
                reducedRadius--;
            }
            interior.Add(new IntVec3(xCenter, 0, zCenter));
        }

        public static List<IntVec3> CircleBorder(int xCenter, int zCenter, int radius)
        {
            HashSet<IntVec3> border = new HashSet<IntVec3>();
            bool foundDiagonal = false;
            int radiusSquared = radius * radius;
            IntVec3 pos = new IntVec3(radius, 0, 0);
            AddOctants(pos, ref border);
            while (!foundDiagonal)
            {
                int left = ((pos.x - 1) * (pos.x - 1)) + (pos.z * pos.z);
                int up = ((pos.z + 1) * (pos.z + 1)) + (pos.x * pos.x);
                if (Math.Abs(radiusSquared - up) > Math.Abs(radiusSquared - left))
                    pos = new IntVec3(pos.x - 1, 0, pos.z);
                else
                    pos = new IntVec3(pos.x, 0, pos.z + 1);
                AddOctants(pos, ref border);
                if (pos.x == pos.z)
                    foundDiagonal = true;
            }
            List<IntVec3> output = new List<IntVec3>();
            foreach (IntVec3 vec in border)
            {
                output.Add(new IntVec3(vec.x + xCenter, 0, vec.z + zCenter));
            }
            return output;
        }

        private static void AddOctants(IntVec3 pos, ref HashSet<IntVec3> border)
        {
            border.Add(pos);
            border.Add(new IntVec3(pos.x * -1, 0, pos.z));
            border.Add(new IntVec3(pos.x, 0, pos.z * -1));
            border.Add(new IntVec3(pos.x * -1, 0, pos.z * -1));
            border.Add(new IntVec3(pos.z, 0, pos.x));
            border.Add(new IntVec3(pos.z * -1, 0, pos.x));
            border.Add(new IntVec3(pos.z, 0, pos.x * -1));
            border.Add(new IntVec3(pos.z * -1, 0, pos.x * -1));
        }

        public static void GenerateImpactSite()
        {
            WorldObject impactSite = WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("ShipEngineImpactSite"));
            int tile = TileFinder.RandomStartingTile();
            impactSite.Tile = tile;
            Find.WorldObjects.Add(impactSite);
        }
    }

    [HarmonyPatch(typeof(ShipUtility), "ShipBuildingsAttachedTo")]
    public static class FindAllTheShipParts
    {
        [HarmonyPrefix]
        public static bool DisableOriginalMethod()
        {
            return false;
        }
        [HarmonyPostfix]
        public static void FindShipPartsReally(Building root, ref List<Building> __result)
        {
            if (root == null || root.Destroyed)
            {
                __result = new List<Building>();
                return;
            }

            var map = root.Map;
            var containedBuildings = new HashSet<Building>();
            var cellsTodo = new HashSet<IntVec3>();
            var cellsDone = new HashSet<IntVec3>();

            cellsTodo.AddRange(GenAdj.CellsOccupiedBy(root));
            cellsTodo.AddRange(GenAdj.CellsAdjacentCardinal(root));

            while (cellsTodo.Count > 0)
            {
                var current = cellsTodo.First();
                cellsTodo.Remove(current);
                cellsDone.Add(current);

                var containedThings = current.GetThingList(map);
                if (!containedThings.Any(thing => (thing as Building)?.def.building.shipPart ?? false))
                {
                    continue;
                }

                foreach (var thing in containedThings)
                {
                    if (thing is Building building)
                    {
                        if (containedBuildings.Add(building))
                        {
                            cellsTodo.AddRange(
                                GenAdj.CellsOccupiedBy(building).
                                Concat(GenAdj.CellsAdjacentCardinal(building)).
                                Where(cell => !cellsDone.Contains(cell))
                            );
                        }
                    }
                }
            }

            __result = containedBuildings.ToList();
        }
    }

    [HarmonyPatch(typeof(ShipUtility), "LaunchFailReasons")]
    public static class FindLaunchFailReasons {
        [HarmonyPrefix]
        public static bool DisableOriginalMethod()
        {
            return false;
        }
        [HarmonyPostfix]
        public static void FindLaunchFailReasonsReally(Building rootBuilding, ref IEnumerable<string> __result)
        {
            List<string> newResult = new List<string>();
            List<Building> shipParts = ShipUtility.ShipBuildingsAttachedTo(rootBuilding);

            if (!FindEitherThing(shipParts, ThingDefOf.Ship_Engine, ThingDef.Named("Ship_Engine_Small"),null))
                newResult.Add("ShipReportMissingPart".Translate() + ": " + ThingDefOf.Ship_Engine.label);
            if (!FindTheThing(shipParts, ThingDefOf.Ship_SensorCluster))
                newResult.Add("ShipReportMissingPart".Translate() + ": " + ThingDefOf.Ship_SensorCluster.label);
            if (!FindEitherThing(shipParts, ThingDef.Named("ShipPilotSeat"), ThingDefOf.Ship_ComputerCore, ThingDef.Named("ShipPilotSeatMini")))
                newResult.Add("ShipReportMissingPart".Translate() + ": " + ThingDef.Named("ShipPilotSeat"));

            float fuelNeeded = 0f;
            float fuelHad = 0f;
            foreach (Building part in shipParts) {
                if (part.def == ThingDefOf.Ship_Engine || part.def.defName.Equals("Ship_Engine_Small")) {
                    if (part.TryGetComp<CompRefuelable>() != null)
                        fuelHad += part.TryGetComp<CompRefuelable>().Fuel;
                }
                if (part.def != ThingDef.Named("ShipHullTile"))
                    fuelNeeded += (part.def.size.x * part.def.size.z) * 3f;
                else
                    fuelNeeded += 1f;
            }
            if (fuelHad < fuelNeeded) {
                newResult.Add("ShipNeedsMoreChemfuel".Translate(fuelHad, fuelNeeded));
            }

            bool hasPilot = false;
            foreach (Building part in shipParts) {
                if ((part.def == ThingDef.Named("ShipPilotSeat") || part.def == ThingDef.Named("ShipPilotSeatMini")) && part.TryGetComp<CompMannable>().MannedNow)
                    hasPilot = true;
                else if (part.def == ThingDefOf.Ship_ComputerCore)
                    hasPilot = true;
            }
            if (!hasPilot) {
                newResult.Add("ShipReportNeedPilot".Translate());
            }

            /*bool fullPodFound = false;
			foreach (Building part in shipParts)
			{
				if (part.def == ThingDefOf.Ship_CryptosleepCasket || part.def == ThingDef.Named("ShipInside_CryptosleepCasket"))
				{
					Building_CryptosleepCasket pod = part as Building_CryptosleepCasket;
					if (pod != null && pod.HasAnyContents)
					{
						fullPodFound = true;
						break;
					}
				}
			}
			if (!fullPodFound)
			{
				__result.Add("ShipReportNoFullPods".Translate());
			}*/

            __result = newResult;
        }

        private static bool FindTheThing(List<Building> shipParts, ThingDef theDef)
        {
            if (!shipParts.Any((Building pa) => pa.def == theDef)) {
                return false;
            }
            return true;
        }

        private static bool FindEitherThing(List<Building> shipParts, ThingDef theDef, ThingDef theOtherDef, ThingDef theThirdDef)
        {
            if (!shipParts.Any((Building pa) => pa.def == theDef) && !shipParts.Any((Building pa) => pa.def == theOtherDef) && !shipParts.Any((Building pa) => pa.def == theThirdDef)) {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ShipCountdown), "CountdownEnded")]
    public static class SaveShip {
        [HarmonyPrefix]
        public static bool SaveShipAndRemoveItemStacks()
        {
            if (ShipInteriorMod2.saveShip) {

                ScreenFader.StartFade(UnityEngine.Color.clear, 1f);

                WorldObjectOrbitingShip orbiter = (WorldObjectOrbitingShip)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("ShipOrbiting"));
                orbiter.radius = 150;
                orbiter.theta = -3;
                orbiter.SetFaction(Faction.OfPlayer);
                for (int i = 0; i < Find.World.grid.TilesCount; i++)
                {
                    if (!Find.World.worldObjects.AnyWorldObjectAt(i))
                    {
                        Log.Message("Generating orbiting ship at tile " + i);
                        orbiter.Tile = i;
                        break;
                    }
                }
                Find.WorldObjects.Add(orbiter);
                Map myMap = MapGenerator.GenerateMap(Find.World.info.initialMapSize, orbiter, orbiter.MapGeneratorDef);
                Map oldMap = ShipInteriorMod2.shipRoot.Map;
                myMap.fogGrid.ClearAllFog();

                /*string shipFolder = Path.Combine (GenFilePaths.SaveDataFolderPath, "Ships");
				DirectoryInfo directoryInfo = new DirectoryInfo(shipFolder);
				if (!directoryInfo.Exists)
				{
					directoryInfo.Create();
				}
				Faction playerFaction = (Current.Game.World.factionManager.AllFactions as IList<Faction>)[Current.Game.World.factionManager.AllFactions.FirstIndexOf ((Faction theFac) => theFac.IsPlayer)];
				string playerFactionName = playerFaction.Name;
				string shipFile = Path.Combine (shipFolder, playerFactionName + ".rwship");*/
                
                /**
                 * This file original created by SoS2 Team. https://steamcommunity.com/sharedfiles/filedetails/?id=1909914131
                 * Any changes that I have applied here are for debugging purposes only.
                 * I hope this does not violate the terms of the license, I will delete this file if the copyright holder requires it.
                 */
                
                // Let start very dirty debug, using println
                // Try to locate place where exception will drop
                Log.Message("Here 1"); // Maybe here?
                
                List<Thing> toSave = new List<Thing>();
                List<Building> shipParts = ShipUtility.ShipBuildingsAttachedTo(ShipInteriorMod2.shipRoot);
                List<Zone> zonesToCopy = new List<Zone>();
                List<Tuple<IntVec3, TerrainDef>> terrainToCopy = new List<Tuple<IntVec3, TerrainDef>>();

                float fuelNeeded = 0f;
                float fuelHad = 0f;
                List<Building> engines = new List<Building>();

                foreach (Thing saveThing in shipParts) {
                    toSave.Add(saveThing);
                    if (saveThing.def == ThingDefOf.Ship_Engine || saveThing.def==ThingDef.Named("Ship_Engine_Small")) {
                        engines.Add((Building)saveThing);
                        if (saveThing.TryGetComp<CompRefuelable>() != null)
                            fuelHad += saveThing.TryGetComp<CompRefuelable>().Fuel;
                    }
                    if (saveThing is Building && saveThing.def != ThingDef.Named("ShipHullTile"))
                        fuelNeeded += (saveThing.def.size.x * saveThing.def.size.z) * 3f;
                    else if (saveThing.def == ThingDef.Named("ShipHullTile"))
                        fuelNeeded += 1f;
                }

                Log.Message("Here 2"); // Or here?
                
                foreach (Building engine in engines) {
                    engine.GetComp<CompRefuelable>().ConsumeFuel(fuelNeeded / engines.Count);
                }

                Log.Message("Here 3"); // Here !
                
                foreach (Building hullTile in shipParts)
                {
                    try // hm, try to add try catch, maybe this fix an issue?
                    {
                        List<Thing> allTheThings = hullTile.Position.GetThingList(hullTile.Map);
                        foreach (Thing theItem in allTheThings) {
                            if (theItem.Map.zoneManager.ZoneAt(theItem.Position) != null && !zonesToCopy.Contains(theItem.Map.zoneManager.ZoneAt(theItem.Position))) {
                                zonesToCopy.Add(theItem.Map.zoneManager.ZoneAt(theItem.Position));
                            }
                            if (!toSave.Contains(theItem)) {
                                toSave.Add(theItem);
                            }
                            UnRoofTilesOverThing(theItem);
                        }
                        if (hullTile.Map.terrainGrid.TerrainAt(hullTile.Position).layerable && !hullTile.Map.terrainGrid.TerrainAt(hullTile.Position).defName.Equals("FakeFloorInsideShip"))
                        {
                            terrainToCopy.Add(new Tuple<IntVec3, TerrainDef>(hullTile.Position, hullTile.Map.terrainGrid.TerrainAt(hullTile.Position)));
                            hullTile.Map.terrainGrid.RemoveTopLayer(hullTile.Position, false);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Message("[HERE 3 ---> part] " + hullTile.def?.defName); // print, which hullTile broke our ship ]:->
                        Log.Error(e.StackTrace); // and exception
                    }
                }

                Log.Message("Here 4"); // Normal
                
                /*SafeSaver.Save (shipFile, "RWShip", delegate
					{
						ScribeMetaHeaderUtility.WriteMetaHeader();
						Scribe_Values.Look<FactionDef>(ref playerFaction.def, "playerFactionDef");
						Scribe.EnterNode("things");
						for(int i=0;i<toSave.Count;i++)
						{
							Thing theThing=toSave[i];
							Scribe_Deep.Look<Thing>(ref theThing, false, "thing", new object[0]);
						}
						Scribe.ExitNode();
						Scribe_Deep.Look<ResearchManager>(ref Current.Game.researchManager, false, "researchManager",new object[0]);
						Scribe_Deep.Look<TaleManager>(ref Current.Game.taleManager, false, "taleManager",new object[0]);
						Scribe_Deep.Look<UniqueIDsManager>(ref Current.Game.World.uniqueIDsManager, false, "uniqueIDsManager",new object[0]);
						Scribe_Deep.Look<TickManager>(ref Current.Game.tickManager, false, "tickManager",new object[0]);
						Scribe_Deep.Look<DrugPolicyDatabase>(ref Current.Game.drugPolicyDatabase, false, "drugPolicyDatabase",new object[0]);
						Scribe_Deep.Look<OutfitDatabase>(ref Current.Game.outfitDatabase, false, "outfitDatabase",new object[0]);
						Scribe_Deep.Look<PlayLog>(ref Current.Game.playLog, false, "playLog",new object[0]);
					});*/
                List<IntVec3> fireExplosions = new List<IntVec3>();
                ShipInteriorMod2.AirlockBugFlag = true;
                foreach (Thing theThing in toSave) {
                    if (!theThing.Destroyed) {
                        if (theThing.def.defName.Equals("Ship_Engine"))
                        {
                            fireExplosions.Add(theThing.Position);
                        }
                        try
                        {
                            theThing.DeSpawn();
                            theThing.SpawnSetup(myMap, false);
                        }
                        catch (Exception e) {
                            Log.Error(e.Message);
                        }
                        //myMap.terrainGrid.SetTerrain (theThing.Position, DefDatabase<TerrainDef>.GetNamed ("FakeFloorInsideShip"));
                    }
                }
                Log.Message("Here 5");  // Normal
                ShipInteriorMod2.AirlockBugFlag = false;
                foreach (IntVec3 pos in fireExplosions)
                {
                    GenExplosion.DoExplosion(pos, Find.CurrentMap, 3.9f, DamageDefOf.Flame, null, -1, -1f, null, null, null, null, null, 0f, 1, false, null, 0f, 1, 0f, false);
                }
                Log.Message("Here 6");  // Normal
                foreach (Zone theZone in zonesToCopy) {
                    oldMap.zoneManager.DeregisterZone(theZone);
                    theZone.zoneManager = myMap.zoneManager;
                    myMap.zoneManager.RegisterZone(theZone);
                }
                Log.Message("Here 7");  // Normal
                foreach (Tuple<IntVec3, TerrainDef> tup in terrainToCopy)
                {
                    if (!myMap.terrainGrid.TerrainAt(tup.Item1).layerable || myMap.terrainGrid.TerrainAt(tup.Item1).defName.Equals("FakeFloorInsideShip"))
                        myMap.terrainGrid.SetTerrain(tup.Item1, tup.Item2);
                }
                Log.Message("Here 8");  // Normal
                typeof(ZoneManager).GetMethod("RebuildZoneGrid", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(myMap.zoneManager, new object[0]);
                typeof(ZoneManager).GetMethod("RebuildZoneGrid", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(oldMap.zoneManager, new object[0]);
                myMap.mapDrawer.RegenerateEverythingNow();
                oldMap.mapDrawer.RegenerateEverythingNow();
                myMap.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
                myMap.temperatureCache.ResetTemperatureCache();
                myMap.weatherManager.TransitionTo(DefDatabase<WeatherDef>.GetNamed("OuterSpaceWeather"));
                foreach (Room room in myMap.regionGrid.allRooms)
                    room.Group.Temperature = 21f;
                Find.LetterStack.ReceiveLetter("LetterLabelOrbitAchieved".Translate(), "LetterOrbitAchieved".Translate(), LetterDefOf.PositiveEvent);
                Log.Message("Here 9");
            }

            return false;
        }

        public static void UnRoofTilesOverThing(Thing t)
        {
            foreach (IntVec3 pos in GenAdj.CellsOccupiedBy(t))
                t.Map.roofGrid.SetRoof(pos, null);
        }
    }

    [HarmonyPatch(typeof(ShipCountdown), "InitiateCountdown", new Type[] { typeof(Building) })]
    public static class InitShipRefs {
        [HarmonyPrefix]
        public static bool SaveStatics(Building launchingShipRoot)
        {
            ShipInteriorMod2.shipRoot = launchingShipRoot;
            ShipInteriorMod2.saveShip = true;
            return true;
        }
    }

    [HarmonyPatch(typeof(Map))]
    [HarmonyPatch("Biome", MethodType.Getter)]
    public static class SpaceBiomeGetter {
        static bool testResult;
        [HarmonyPrefix]
        public static bool interceptBiome(Map __instance)
        {
            testResult = false;
            if (__instance.info != null && __instance.info.parent != null && (__instance.info.parent is WorldObjectOrbitingShip || __instance.info.parent is SpaceSite))
                testResult = true;
            return !testResult;
        }

        [HarmonyPostfix]
        public static void getSpaceBiome(Map __instance, ref BiomeDef __result)
        {
            if (testResult)
                __result = ShipInteriorMod2.OuterSpaceBiome;
        }
    }

    [HarmonyPatch(typeof(MapTemperature))]
    [HarmonyPatch("OutdoorTemp", MethodType.Getter)]
    public static class FixOutdoorTemp {
        [HarmonyPostfix]
        public static void getSpaceTemp(ref float __result, MapTemperature __instance)
        {
            if (((Map)typeof(MapTemperature).GetField("map", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance)).Biome == ShipInteriorMod2.OuterSpaceBiome)
                __result = -100f;
        }
    }

    [HarmonyPatch(typeof(MapTemperature))]
    [HarmonyPatch("SeasonalTemp", MethodType.Getter)]
    public static class FixSeasonalTemp {
        [HarmonyPostfix]
        public static void getSpaceTemp(ref float __result, MapTemperature __instance)
        {
            if (((Map)typeof(MapTemperature).GetField("map", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance)).Biome == ShipInteriorMod2.OuterSpaceBiome)
                __result = -100f;
        }
    }

    /*[HarmonyPatch(typeof(RoomGroupTempTracker),"EqualizeTemperature")]
	public static class ShipLifeSupport{
		[HarmonyPostfix]
		public static void setShipTemp(RoomGroupTempTracker __instance)
		{
			RoomGroup theGroup = (RoomGroup)typeof(RoomGroupTempTracker).GetField ("roomGroup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue (__instance);
			foreach (Room theRoom in theGroup.Rooms) {
				if (theRoom.Role == DefDatabase<RoomRoleDef>.GetNamed ("ShipInside") && theRoom.OpenRoofCount<=0 && hasLifeSupport(theRoom))
					__instance.Temperature = 21f;
			}
		}

		static bool hasLifeSupport(Room theRoom)
		{
			return theRoom.Map.spawnedThings.Any (t => t.def.defName.Equals ("Ship_LifeSupport") && ((ThingWithComps)t).GetComp<CompFlickable> ().SwitchIsOn && ((ThingWithComps)t).GetComp<CompPowerTrader> ().PowerOn);
		}
	}*/

    [HarmonyPatch(typeof(RoomGroupTempTracker), "EqualizeTemperature")]
    public static class ExposedToVacuum
    {
        [HarmonyPostfix]
        public static void setShipTemp(RoomGroupTempTracker __instance)
        {
            RoomGroup theGroup = (RoomGroup)typeof(RoomGroupTempTracker).GetField("roomGroup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
            if (theGroup.Map.terrainGrid.TerrainAt(IntVec3.Zero).defName != "EmptySpace")
                return;
            foreach (Room theRoom in theGroup.Rooms)
            {
                if (theRoom.Role != RoomRoleDefOf.None && (theRoom.Role != DefDatabase<RoomRoleDef>.GetNamed("ShipInside") || theRoom.OpenRoofCount > 0))
                    __instance.Temperature = -100f;
            }
        }
    }

    [HarmonyPatch(typeof(Fire), "DoComplexCalcs")]
    public static class CannotBurnInSpace {
        [HarmonyPostfix]
        public static void extinguish(Fire __instance)
        {
            if (__instance.Spawned && __instance.Map.Biome == ShipInteriorMod2.OuterSpaceBiome && (__instance.Position.GetRoom(__instance.Map) == null || (__instance.Position.GetRoom(__instance.Map).OpenRoofCount > 0 || __instance.Position.GetRoom(__instance.Map).UsesOutdoorTemperature)))
                __instance.TakeDamage(new DamageInfo(DamageDefOf.Extinguish, 100, 0, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null));
        }
    }

    [HarmonyPatch(typeof(Plant), "TickLong")]
    public static class KillThePlantsInSpace {
        [HarmonyPostfix]
        public static void extinguish(Plant __instance)
        {
            if (__instance.Spawned && __instance.Map.Biome == ShipInteriorMod2.OuterSpaceBiome && (__instance.Position.GetRoom(__instance.Map) == null || (__instance.Position.GetRoom(__instance.Map).OpenRoofCount > 0 || __instance.Position.GetRoom(__instance.Map).UsesOutdoorTemperature)))
            {
                __instance.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, 10, 0, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null));
            }
        }
    }

    [HarmonyPatch(typeof(Page_SelectStartingSite), "CanDoNext")]
    public static class LetMeLandOnMyOwnBase
    {
        [HarmonyPrefix]
        public static bool Nope()
        {
            return false;
        }

        [HarmonyPostfix]
        public static void CanLandPlz(ref bool __result)
        {
            int selectedTile = Find.WorldInterface.SelectedTile;
            if (selectedTile < 0)
            {
                Messages.Message("MustSelectLandingSite".Translate(), MessageTypeDefOf.RejectInput);
                __result = false;
            }
            else
            {
                StringBuilder stringBuilder = new StringBuilder();
                if (!TileFinder.IsValidTileForNewSettlement(selectedTile, stringBuilder) && (Find.World.worldObjects.SettlementAt(selectedTile) == null || Find.World.worldObjects.SettlementAt(selectedTile).Faction != Faction.OfPlayer))
                {
                    Messages.Message(stringBuilder.ToString(), MessageTypeDefOf.RejectInput);
                    __result = false;
                }
                else
                {
                    Tile tile = Find.WorldGrid[selectedTile];
                    __result = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Scenario), "PostWorldGenerate")]
    public static class SelectiveWorldGeneration
    {
        [HarmonyPrefix]
        public static bool Replace(Scenario __instance)
        {
            if (WorldSwitchUtility.SelectiveWorldGenFlag)
            {
                Current.ProgramState = ProgramState.MapInitializing;
                FactionGenerator.EnsureRequiredEnemies(Find.GameInitData.playerFaction);
                Current.ProgramState = ProgramState.Playing;

                WorldSwitchUtility.SoonToBeObsoleteWorld.worldPawns = null;
                WorldSwitchUtility.SoonToBeObsoleteWorld.factionManager = null;
                WorldComponent obToRemove = null;
                List<UtilityWorldObject> uwos = new List<UtilityWorldObject>();
                foreach (WorldObject ob in WorldSwitchUtility.SoonToBeObsoleteWorld.worldObjects.AllWorldObjects)
                {
                    if (ob is UtilityWorldObject && !(ob is PastWorldUWO))
                        uwos.Add((UtilityWorldObject)ob);
                }
                foreach (WorldComponent comp in WorldSwitchUtility.SoonToBeObsoleteWorld.components)
                {
                    if (comp is PastWorldUWO2)
                        obToRemove = comp;
                }
                WorldSwitchUtility.SoonToBeObsoleteWorld.components.Remove(obToRemove);
                foreach (UtilityWorldObject uwo in uwos)
                {
                    ((List<WorldObject>)typeof(WorldObjectsHolder).GetField("worldObjects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(WorldSwitchUtility.SoonToBeObsoleteWorld.worldObjects)).Remove(uwo);
                    typeof(WorldObjectsHolder).GetMethod("RemoveFromCache", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(WorldSwitchUtility.SoonToBeObsoleteWorld.worldObjects, new object[] { uwo });

                }

                List<WorldComponent> modComps = new List<WorldComponent>();
                foreach (WorldComponent comp in WorldSwitchUtility.SoonToBeObsoleteWorld.components)
                {
                    if (!(comp is TileTemperaturesComp) && !(comp is WorldGenData) && !(comp is PastWorldUWO2))
                        modComps.Add(comp);
                }
                foreach (WorldComponent comp in modComps)
                    WorldSwitchUtility.SoonToBeObsoleteWorld.components.Remove(comp);

                if (!WorldSwitchUtility.planetkiller)
                    WorldSwitchUtility.PastWorldTracker.PastWorlds.Add(WorldSwitchUtility.PreviousWorldFromWorld(WorldSwitchUtility.SoonToBeObsoleteWorld));
                else
                    WorldSwitchUtility.planetkiller = false;

                Find.World.components.Remove(Find.World.components.Where(c => c is PastWorldUWO2).FirstOrDefault());
                Find.World.components.Add(WorldSwitchUtility.PastWorldTracker);
                foreach (UtilityWorldObject uwo in uwos)
                {
                    Find.WorldObjects.Add(uwo);
                }
                WorldComponent toReplace;
                foreach (WorldComponent comp in modComps)
                {
                    toReplace = null;
                    foreach (WorldComponent otherComp in Find.World.components)
                    {
                        if (otherComp.GetType() == comp.GetType())
                            toReplace = otherComp;
                    }
                    if (toReplace != null)
                        Find.World.components.Remove(toReplace);
                    Find.World.components.Add(comp);
                }

                WorldSwitchUtility.SelectiveWorldGenFlag = false;
                WorldSwitchUtility.CacheFactions(Current.CreatingWorld.info.name);
                WorldSwitchUtility.RespawnShip();

                RenderPlanetBehindMap.renderedThatAlready = false;

                //Prevent forced events from firing during the intervening years
                foreach (ScenPart part in Find.Scenario.AllParts)
                {
                    if (part.def.defName.Equals("CreateIncident"))
                    {
                        Type createIncident = typeof(ScenPart).Assembly.GetType("RimWorld.ScenPart_CreateIncident");
                        createIncident.GetField("occurTick", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(part, (float)createIncident.GetProperty("IntervalTicks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(part, null) + Current.Game.tickManager.TicksAbs);
                    }
                }
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(WorldGenStep_Factions), "GenerateFresh")]
    public static class SelectiveWorldGenerationToo
    {
        [HarmonyPrefix]
        public static bool DontReplace()
        {
            if (WorldSwitchUtility.SelectiveWorldGenFlag) {
                Find.GameInitData.playerFaction = WorldSwitchUtility.SavedPlayerFaction;
                Find.World.factionManager.Add(WorldSwitchUtility.SavedPlayerFaction);
            }
            return true;
        }

        [HarmonyPostfix]
        public static void LoadNow()
        {
            if (WorldSwitchUtility.SelectiveWorldGenFlag)
            {
                WorldSwitchUtility.LoadUniqueIDsFactionsAndWorldPawns();
                foreach (Faction fac in Find.FactionManager.AllFactions)
                {
                    if (fac.def.hidden)
                    {
                        foreach (Faction fac2 in Find.FactionManager.AllFactions)
                        {
                            if (fac != fac2)
                            {
                                fac.TryMakeInitialRelationsWith(fac2);
                            }
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(FactionGenerator), "GenerateFactionsIntoWorld")]
    public static class DontRegenerateHiddenFactions
    {
        [HarmonyPrefix]
        public static bool PossiblyReplace()
        {
            if (WorldSwitchUtility.SelectiveWorldGenFlag)
            {
                WorldSwitchUtility.FactionRelationFlag = true;
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        public static void Replace()
        {
            if (WorldSwitchUtility.SelectiveWorldGenFlag)
            {
                int i = 0;
                foreach (FactionDef current in DefDatabase<FactionDef>.AllDefs)
                {
                    for (int j = 0; j < current.requiredCountAtGameStart; j++)
                    {
                        if (!current.hidden)
                        {
                            Faction faction = FactionGenerator.NewGeneratedFaction(current);
                            Find.FactionManager.Add(faction);
                            i++;
                        }
                    }
                }
                while (i < 5)
                {
                    FactionDef facDef = (from fa in DefDatabase<FactionDef>.AllDefs
                                         where fa.canMakeRandomly && Find.FactionManager.AllFactions.Count((Faction f) => f.def == fa) < fa.maxCountAtGameStart
                                         select fa).RandomElement<FactionDef>();
                    Faction faction2 = FactionGenerator.NewGeneratedFaction(facDef);
                    Find.World.factionManager.Add(faction2);
                    i++;
                }
                int num = GenMath.RoundRandom((float)Find.WorldGrid.TilesCount / 100000f * new FloatRange(75f, 85f).RandomInRange);
                num -= Find.WorldObjects.Settlements.Count;
                for (int k = 0; k < num; k++)
                {
                    Faction faction3 = (from x in Find.World.factionManager.AllFactionsListForReading
                                        where !x.def.isPlayer && !x.def.hidden
                                        select x).RandomElementByWeight((Faction x) => x.def.settlementGenerationWeight);
                    Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                    settlement.SetFaction(faction3);
                    settlement.Tile = TileFinder.RandomSettlementTileFor(faction3, false, null);
                    settlement.Name = SettlementNameGenerator.GenerateSettlementName(settlement, null);
                    Find.WorldObjects.Add(settlement);
                }
                WorldSwitchUtility.FactionRelationFlag = false;
            }
        }
    }

    [HarmonyPatch(typeof(Page_SelectScenario), "BeginScenarioConfiguration")]
    public static class DoNotWipeGame
    {
        [HarmonyPrefix]
        public static bool UseTheFlag()
        {
            if (WorldSwitchUtility.SelectiveWorldGenFlag)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MainMenuDrawer), "Init")]
    public static class SelectiveWorldGenCancel
    {
        [HarmonyPrefix]
        public static bool CancelFlag()
        {
            WorldSwitchUtility.SelectiveWorldGenFlag = false;
            return true;
        }
    }

    [HarmonyPatch(typeof(Page), "CanDoBack")]
    public static class NoGoingBackWhenMakingANewScenario
    {
        [HarmonyPostfix]
        public static void Nope(ref bool __result)
        {
            if (WorldSwitchUtility.SelectiveWorldGenFlag)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(CompShipPart), "CompGetGizmosExtra")]
    public static class NoGizmoInSpace
    {
        static bool inSpace;
        [HarmonyPrefix]
        public static bool CheckBiome(CompShipPart __instance)
        {
            inSpace = false;
            if (__instance.parent.Map != null && __instance.parent.Map.Biome == ShipInteriorMod2.OuterSpaceBiome)
            {
                inSpace = true;
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        public static void ReturnEmpty(ref IEnumerable<Gizmo> __result)
        {
            if (inSpace)
                __result = new List<Gizmo>();
        }
    }

    [HarmonyPatch(typeof(Building_CryptosleepCasket), "FindCryptosleepCasketFor")]
    public static class AllowCrittersleepCaskets
    {
        [HarmonyPrefix]
        public static bool BlockExecution()
        {
            return false;
        }

        [HarmonyPostfix]
        public static void CrittersCanSleepToo(ref Building_CryptosleepCasket __result, Pawn p, Pawn traveler, bool ignoreOtherReservations = false)
        {
            IEnumerable<ThingDef> enumerable = from def in DefDatabase<ThingDef>.AllDefs
                                               where typeof(Building_CryptosleepCasket).IsAssignableFrom(def.thingClass)
                                               select def;
            foreach (ThingDef current in enumerable)
            {
                if (current == ThingDef.Named("Cryptonest"))
                    continue;
                Building_CryptosleepCasket building_CryptosleepCasket = (Building_CryptosleepCasket)GenClosest.ClosestThingReachable(p.Position, p.Map, ThingRequest.ForDef(current), PathEndMode.InteractionCell, TraverseParms.For(traveler, Danger.Deadly, TraverseMode.ByPawn, false), 9999f, delegate (Thing x)
                    {
                        bool arg_33_0;
                        if ((x.def.defName == "CrittersleepCasket" && p.BodySize <= ShipInteriorMod2.crittersleepBodySize && ((ThingOwner)typeof(Building_CryptosleepCasket).GetField("innerContainer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue((Building_CryptosleepCasket)x)).Count < 8) || (x.def.defName == "CrittersleepCasketLarge" && p.BodySize <= ShipInteriorMod2.crittersleepBodySize && ((ThingOwner)typeof(Building_CryptosleepCasket).GetField("innerContainer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue((Building_CryptosleepCasket)x)).Count < 32))
                        {
                            Pawn traveler2 = traveler;
                            LocalTargetInfo target = x;
                            bool ignoreOtherReservations2 = ignoreOtherReservations;
                            arg_33_0 = traveler2.CanReserve(target, 1, -1, null, ignoreOtherReservations2);
                        }
                        else
                        {
                            arg_33_0 = false;
                        }
                        return arg_33_0;
                    }, null, 0, -1, false, RegionType.Set_Passable, false);
                if (building_CryptosleepCasket != null)
                {
                    __result = building_CryptosleepCasket;
                    return;
                }
                building_CryptosleepCasket = (Building_CryptosleepCasket)GenClosest.ClosestThingReachable(p.Position, p.Map, ThingRequest.ForDef(current), PathEndMode.InteractionCell, TraverseParms.For(traveler, Danger.Deadly, TraverseMode.ByPawn, false), 9999f, delegate (Thing x)
                {
                    bool arg_33_0;
                    if ((x.def.defName != "CrittersleepCasketLarge" && x.def.defName != "CrittersleepCasket" && !((Building_CryptosleepCasket)x).HasAnyContents))
                    {
                        Pawn traveler2 = traveler;
                        LocalTargetInfo target = x;
                        bool ignoreOtherReservations2 = ignoreOtherReservations;
                        arg_33_0 = traveler2.CanReserve(target, 1, -1, null, ignoreOtherReservations2);
                    }
                    else
                    {
                        arg_33_0 = false;
                    }
                    return arg_33_0;
                }, null, 0, -1, false, RegionType.Set_Passable, false);
                if (building_CryptosleepCasket != null)
                {
                    __result = building_CryptosleepCasket;
                }
            }
        }
    }

    [HarmonyPatch(typeof(JobDriver_CarryToCryptosleepCasket), "MakeNewToils")]
    public static class JobDriverFix
    {
        [HarmonyPrefix]
        public static bool BlockExecution()
        {
            return false;
        }

        [HarmonyPostfix]
        public static void FillThatCasket(ref IEnumerable<Toil> __result, JobDriver_CarryToCryptosleepCasket __instance)
        {
            Pawn Takee = (Pawn)typeof(JobDriver_CarryToCryptosleepCasket).GetMethod("get_Takee", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, new object[0]);
            Building_CryptosleepCasket DropPod = (Building_CryptosleepCasket)typeof(JobDriver_CarryToCryptosleepCasket).GetMethod("get_DropPod", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, new object[0]);
            List<Toil> myResult = new List<Toil>();
            __instance.FailOnDestroyedOrNull(TargetIndex.A);
            __instance.FailOnDestroyedOrNull(TargetIndex.B);
            __instance.FailOnAggroMentalState(TargetIndex.A);
            __instance.FailOn(() => !DropPod.Accepts(Takee));
            myResult.Add(Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOn(() => (DropPod.def.defName != "CrittersleepCasket" && DropPod.def.defName != "CrittersleepCasketLarge") && DropPod.GetDirectlyHeldThings().Count > 0).FailOn(() => !Takee.Downed).FailOn(() => !__instance.pawn.CanReach(Takee, PathEndMode.OnCell, Danger.Deadly, false, TraverseMode.ByPawn)).FailOnSomeonePhysicallyInteracting(TargetIndex.A));
            myResult.Add(Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false));
            myResult.Add(Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.InteractionCell));
            Toil prepare = Toils_General.Wait(500);
            prepare.FailOnCannotTouch(TargetIndex.B, PathEndMode.InteractionCell);
            prepare.WithProgressBarToilDelay(TargetIndex.B, false, -0.5f);
            myResult.Add(prepare);
            myResult.Add(new Toil
            {
                initAction = delegate
                {
                    DropPod.TryAcceptThing(Takee, true);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            });
            __result = myResult;
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddHumanlikeOrders")]
    public static class EggFix
    {
        [HarmonyPostfix]
        public static void FillThatNest(Vector3 clickPos, Pawn pawn, ref List<FloatMenuOption> opts)
        {
            if (pawn == null || clickPos == null)
                return;
            IntVec3 c = IntVec3.FromVector3(clickPos);
            if (pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) {
                foreach (Thing current in c.GetThingList(pawn.Map)) {
                    if (current.def.IsWithinCategory(ThingCategoryDef.Named("EggsFertilized")) && pawn.CanReserveAndReach(current, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, true) && findCryptonestFor(current, pawn, true) != null) {
                        string text2 = "Carry to cryptonest";
                        JobDef jDef = DefDatabase<JobDef>.GetNamed("CarryToCryptonest");
                        Action action2 = delegate
                        {
                            Building_CryptosleepCasket building_CryptosleepCasket = findCryptonestFor(current, pawn, false);
                            if (building_CryptosleepCasket == null)
                            {
                                building_CryptosleepCasket = findCryptonestFor(current, pawn, true);
                            }
                            if (building_CryptosleepCasket == null)
                            {
                                Messages.Message("CannotCarryToCryptosleepCasket".Translate() + ": " + "NoCryptosleepCasket".Translate(), current, MessageTypeDefOf.RejectInput);
                                return;
                            }
                            Job job = new Job(jDef, current, building_CryptosleepCasket);
                            job.count = current.stackCount;
                            int eggsAlreadyInNest = (typeof(Building_CryptosleepCasket).GetField("innerContainer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(building_CryptosleepCasket) as ThingOwner).Count;
                            if (job.count + eggsAlreadyInNest > 16)
                                job.count = 16 - eggsAlreadyInNest;
                            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                        };
                        string label = text2;
                        Action action = action2;
                        opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action, MenuOptionPriority.Default, null, current, 0f, null, null), pawn, current, "ReservedBy"));
                    }
                }
            }
        }

        static Building_CryptosleepCasket findCryptonestFor(Thing egg, Pawn p, bool ignoreOtherReservations)
        {
            Building_CryptosleepCasket building_CryptosleepCasket = (Building_CryptosleepCasket)GenClosest.ClosestThingReachable(p.Position, p.Map, ThingRequest.ForDef(ThingDef.Named("Cryptonest")), PathEndMode.InteractionCell, TraverseParms.For(p, Danger.Deadly, TraverseMode.ByPawn, false), 9999f, delegate (Thing x)
                {
                    bool arg_33_0;
                    if (((ThingOwner)typeof(Building_CryptosleepCasket).GetField("innerContainer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue((Building_CryptosleepCasket)x)).TotalStackCount < 16)
                    {
                        LocalTargetInfo target = x;
                        bool ignoreOtherReservations2 = ignoreOtherReservations;
                        arg_33_0 = p.CanReserve(target, 1, -1, null, ignoreOtherReservations2);
                    }
                    else
                    {
                        arg_33_0 = false;
                    }
                    return arg_33_0;
                }, null, 0, -1, false, RegionType.Set_Passable, false);
            if (building_CryptosleepCasket != null)
            {
                return building_CryptosleepCasket;
            }
            return null;
        }
    }

    /*[HarmonyPatch(typeof(Building),"GetGizmos")]
	public static class GizmoFix{
		[HarmonyPostfix]
		public static void GetAllTheGizmos (Building __instance, ref IEnumerable<Gizmo> __result)
		{
			if (!(__instance.GetType ().Name.EqualsIgnoreCase ("RimWorld.Building_ShipComputerCore")))
				return;

			List<Gizmo> newList = new List<Gizmo> ();
			foreach (Gizmo g in __result) {
				newList.Add (g);
			}

			if (__instance.Map.Biome == BiomeDef.Named("OuterSpaceBiome")) {*/
    /*yield return new Command_Action {
        defaultLabel = "Land Ship",
        defaultDesc = "Land the ship on the currently orbited planet",
        icon = ContentFinder<Texture2D>.Get ("UI/Commands/LaunchShip", true),
        action = delegate {
            Current.Game.InitData = new GameInitData();
            Find.WorldInterface.selector.ClearSelection();
            Page thePage=new Page_SelectLandingSite();
            thePage.next=null;
            thePage.nextAct=delegate {
                LandMe();
            };
            Find.WindowStack.Add(thePage);
        }
    };*/
    /*newList.Add(new Command_Action {
        defaultLabel = "Go to new planet",
        defaultDesc = "Leave this planet behind and find a new rimworld to settle",
        icon = ContentFinder<Texture2D>.Get ("UI/Commands/LaunchShip", true),
        action = delegate {
            WorldSwitchUtility.ColonyAbandonWarning(delegate { WorldSwitchUtility.SwitchToNewWorld(__instance.Map); });
        }
    });

    if (WorldSwitchUtility.PastWorldTracker != null && WorldSwitchUtility.PastWorldTracker.PastWorlds.Count > 0) {
        newList.Add(new Command_Action{
            defaultLabel = "Return to visited planet",
            defaultDesc="Return to a planet you have previously visited",
            icon = ContentFinder<Texture2D>.Get ("UI/Commands/LaunchShip", true),
            action = delegate {
                WorldSwitchUtility.ColonyAbandonWarning(delegate { WorldSwitchUtility.ReturnToPreviousWorld(__instance.Map); });
            }
        });
    }
}

__result = newList;
}
}*/

    /*[HarmonyPatch(typeof(CompShipPart),"PostSpawnSetup")]
	public static class RemoveVacuum{
		[HarmonyPostfix]
		public static void GetRidOfVacuum (CompShipPart __instance)
		{
			if (__instance.parent.Map.terrainGrid.TerrainAt (__instance.parent.Position).defName.Equals ("EmptySpace"))
				__instance.parent.Map.terrainGrid.SetTerrain (__instance.parent.Position,TerrainDef.Named("FakeFloorInsideShip"));
		}
	}*/

    [HarmonyPatch(typeof(Building_Casket), "Tick")]
    public static class EggsDontHatch {
        [HarmonyPrefix]
        public static bool Nope(Building_Casket __instance)
        {
            if (__instance.def.defName.Equals("Cryptonest"))
            {
                List<ThingComp> comps = (List<ThingComp>)typeof(ThingWithComps).GetField("comps", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
                if (comps != null)
                {
                    int i = 0;
                    int count = comps.Count;
                    while (i < count)
                    {
                        comps[i].CompTick();
                        i++;
                    }
                }
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Building_CryptosleepCasket), "GetFloatMenuOptions")]
    public static class CantEnterCryptonest {
        [HarmonyPrefix]
        public static bool Nope(Building_CryptosleepCasket __instance)
        {
            if (__instance.def.defName.Equals("Cryptonest"))
            {
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        public static void AlsoNope(IEnumerable<FloatMenuOption> __result, Building_CryptosleepCasket __instance)
        {
            if (__instance.def.defName.Equals("Cryptonest"))
            {
                __result = new List<FloatMenuOption>();
            }
        }
    }

    [HarmonyPatch(typeof(Dialog_LoadTransporters), "AddPawnsToTransferables", null)]
    public static class TransportPrisoners_Patch
    {
        [HarmonyPrefix]
        public static bool DownedPawns_AddToTransferables(Dialog_LoadTransporters __instance)
        {
            List<Pawn> list = CaravanFormingUtility.AllSendablePawns((Map)typeof(Dialog_LoadTransporters).GetField("map", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance), true, true);
            for (int i = 0; i < list.Count; i++)
            {
                typeof(Dialog_LoadTransporters).GetMethod("AddToTransferables", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[1] { list[i] });
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ShortCircuitUtility), "DoShortCircuit")]
    public static class NoShortCircuitCapacitors
    {
        static bool shortPrevented;

        [HarmonyPrefix]
        public static bool disableEventQuestionMark(Building culprit)
        {
            shortPrevented = false;
            PowerNet powerNet = culprit.PowerComp.PowerNet;
            if (powerNet.batteryComps.Any((CompPowerBattery x) => x.parent.def == ThingDef.Named("ShipCapacitor"))) {
                shortPrevented = true;
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        public static void tellThePlayerTheDayWasSaved(Building culprit)
        {
            if (shortPrevented) {
                string text = "A fault in an electrical conduit has caused a short circuit. Luckily, your capacitor array's fuse prevented it from discharging.";
                Find.LetterStack.ReceiveLetter("LetterLabelShortCircuit".Translate(), text, LetterDefOf.NegativeEvent, new TargetInfo(culprit.Position, culprit.Map, false), null);
            }
        }
    }

    [HarmonyPatch(typeof(Game), "LoadGame")]
    public static class LoadPreviousWorlds
    {
        [HarmonyPrefix]
        public static bool PurgeIt()
        {
            WorldSwitchUtility.PurgePWT();
            return true;
        }
    }

    [HarmonyPatch(typeof(RoomGroupTempTracker), "WallEqualizationTempChangePerInterval")]
    public static class TemperatureDoesntDiffuseFastInSpace
    {
        [HarmonyPostfix]
        public static void RadiativeHeatTransferOnly(ref float __result, RoomGroupTempTracker __instance)
        {
            if (((RoomGroup)typeof(RoomGroupTempTracker).GetField("roomGroup", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance)).Map.terrainGrid.TerrainAt(IntVec3.Zero).defName == "EmptySpace")
            {
                __result *= 0.01f;
            }
        }
    }

    [HarmonyPatch(typeof(RoomGroupTempTracker), "ThinRoofEqualizationTempChangePerInterval")]
    public static class TemperatureDoesntDiffuseFastInSpaceToo
    {
        [HarmonyPostfix]
        public static void RadiativeHeatTransferOnly(ref float __result, RoomGroupTempTracker __instance)
        {
            if (((RoomGroup)typeof(RoomGroupTempTracker).GetField("roomGroup", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance)).Map.terrainGrid.TerrainAt(IntVec3.Zero).defName == "EmptySpace")
            {
                __result *= 0.01f;
            }
        }
    }

    [HarmonyPatch(typeof(Skyfaller), "HitRoof")]
    public static class ShuttleBayAcceptsShuttle
    {
        [HarmonyPrefix]
        public static bool NoHitRoof(Skyfaller __instance)
        {
            if (__instance.Position.GetThingList(__instance.Map).Any(t => t.def.defName.Equals("ShipShuttleBay") || t.def.defName.Equals("ShipSalvageBay")))
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(TransportPodsArrivalActionUtility), "DropTravelingTransportPods")]
    public static class ShuttleBayArrivalPrecision
    {
        [HarmonyPrefix]
        public static bool LandInBay(List<ActiveDropPodInfo> dropPods, IntVec3 near, Map map)
        {
            if (map.Parent != null && map.Parent.def.defName.Equals("ShipOrbiting"))
            {
                TransportPodsArrivalActionUtility.RemovePawnsFromWorldPawns(dropPods);
                for (int i = 0; i < dropPods.Count; i++)
                {
                    DropPodUtility.MakeDropPodAt(near, map, dropPods[i]);
                }
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(World), "GetUniqueLoadID")]
    public static class FixLoadID
    {
        [HarmonyPostfix]
        public static void NewID(World __instance, ref string __result)
        {
            __result = "World" + __instance.info.name;
        }
    }

    [HarmonyPatch(typeof(FactionManager))]
    [HarmonyPatch("AllFactionsVisible", MethodType.Getter)]
    public static class OnlyThisPlanetsVisibleFactions
    {
        [HarmonyPostfix]
        public static void FilterTheFactions(ref IEnumerable<Faction> __result)
        {
            if (Current.ProgramState == ProgramState.Playing)
                __result = WorldSwitchUtility.FactionsOnCurrentWorld(__result).Where(x => !x.def.hidden);
        }
    }

    [HarmonyPatch(typeof(FactionManager))]
    [HarmonyPatch("AllFactions", MethodType.Getter)]
    public static class OnlyThisPlanetsFactions
    {
        [HarmonyPostfix]
        public static void FilterTheFactions(ref IEnumerable<Faction> __result)
        {
            __result = WorldSwitchUtility.FactionsOnCurrentWorld(__result);
        }
    }

    [HarmonyPatch(typeof(FactionManager))]
    [HarmonyPatch("AllFactionsInViewOrder", MethodType.Getter)]
    public static class OnlyThisPlanetsFactionsInViewOrder
    {
        [HarmonyPostfix]
        public static void FilterTheFactions(ref IEnumerable<Faction> __result)
        {
            __result = FactionManager.GetInViewOrder(WorldSwitchUtility.FactionsOnCurrentWorld(__result));
        }
    }

    [HarmonyPatch(typeof(FactionManager))]
    [HarmonyPatch("AllFactionsVisibleInViewOrder", MethodType.Getter)]
    public static class OnlyThisPlanetsFactionsVisibleInViewOrder
    {
        [HarmonyPostfix]
        public static void FilterTheFactions(ref IEnumerable<Faction> __result)
        {
            __result = FactionManager.GetInViewOrder(WorldSwitchUtility.FactionsOnCurrentWorld(__result)).Where(x => !x.def.hidden);
        }
    }

    [HarmonyPatch(typeof(FactionManager))]
    [HarmonyPatch("AllFactionsListForReading", MethodType.Getter)]
    public static class OnlyThisPlanetsFactionsForReading
    {
        [HarmonyPostfix]
        public static void FilterTheFactions(ref IEnumerable<Faction> __result)
        {
            __result = WorldSwitchUtility.FactionsOnCurrentWorld(__result);
        }
    }

    [HarmonyPatch(typeof(FactionManager))]
    [HarmonyPatch("FirstFactionOfDef")]
    public static class OnlyThisPlanetsFirstFactions
    {
        [HarmonyPostfix]
        public static void FilterTheFactions(ref Faction __result, FactionDef facDef)
        {
            __result = Find.FactionManager.AllFactions.Where(x => x.def == facDef).FirstOrDefault();
        }
    }

    [HarmonyPatch(typeof(FactionManager), "RecacheFactions")]
    public static class NoRecache
    {
        [HarmonyPrefix]
        public static bool CheckFlag()
        {
            return !WorldSwitchUtility.NoRecache;
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "SetupMoveIntoNextCell")]
    public static class SpaceZoomies
    {
        [HarmonyPostfix]
        public static void GoFast(Pawn_PathFollower __instance)
        {
            Pawn p = (Pawn)typeof(Pawn_PathFollower).GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
            if (p.Map.terrainGrid.TerrainAt(__instance.nextCell).defName.Equals("EmptySpace") && ShipInteriorMod2.hasSpaceSuit(p))
            {
                __instance.nextCellCostLeft /= 4;
                __instance.nextCellCostTotal /= 4;
            }
        }
    }

    /*[HarmonyPatch(typeof(Pawn_PlayerSettings))]
    [HarmonyPatch("EffectiveAreaRestrictionInPawnCurrentMap",MethodType.Getter)]
    public static class StayOuttaVacuum
    {
        [HarmonyPostfix]
        public static void PawnsDontWantToExplode(ref Area __result, Pawn_PlayerSettings __instance)
        {
            Log.Message("Checkin out a pawn");
            Pawn p = (Pawn)typeof(Pawn_PlayerSettings).GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
            if(p.Map != null && p.Map.Biome.defName.Equals("OuterSpaceBiome") && p.RaceProps.IsFlesh && !ShipInteriorMod2.hasSpaceSuit(p))
            {
                if (__result.Label != null && __result.Label.Equals("AvoidTheVoid"))
                    return;
                Log.Message("Should probably avoid vacuum");
                Area_Allowed areaMinusVacuum = new Area_Allowed();
                BoolGrid theGrid = new BoolGrid(p.Map);
                typeof(Area).GetField("innerGrid", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(areaMinusVacuum, theGrid);
                if (__result == null)
                {
                    Log.Message("Building a new area");
                    foreach (IntVec3 cell in p.Map.AllCells)
                    {
                        Region r = p.Map.regionGrid.GetRegionAt_NoRebuild_InvalidAllowed(cell);
                        if (r != null)
                        {
                            Room room = r.Room;
                            if (room != null && room.Role.defName.Equals("ShipInside"))
                                theGrid[cell] = true;
                        }
                    }
                }
                else
                {
                    Log.Message("Looking for area overlap");
                    foreach (IntVec3 cell in p.Map.AllCells)
                    {
                        if (__result[cell])
                        {
                            Region r = p.Map.regionGrid.GetRegionAt_NoRebuild_InvalidAllowed(cell);
                            if (r != null)
                            {
                                Room room = r.Room;
                                if (room != null && room.Role.defName.Equals("ShipInside"))
                                    theGrid[cell] = true;
                            }
                        }
                    }
                }
                areaMinusVacuum.SetLabel("AvoidTheVoid");
                __result = areaMinusVacuum;
            }
        }
    }*/

    [HarmonyPatch(typeof(Region), "Allows")]
    public static class VacuumIsNotFun
    {
        [HarmonyPostfix]
        public static void AccurateAssessmentOfDanger(TraverseParms tp, Region __instance, ref bool __result, bool isDestination)
        {
            if (tp.pawn == null || !isDestination)
                return;
            if (tp.pawn.Map.Biome == ShipInteriorMod2.OuterSpaceBiome && tp.pawn.RaceProps.IsFlesh && !ShipInteriorMod2.hasSpaceSuit(tp.pawn) && (__instance.Room == null || __instance.touchesMapEdge || (!__instance.Room.Role.defName.Equals("ShipInside") && __instance.type != RegionType.Portal)))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(ExitMapGrid))]
    [HarmonyPatch("MapUsesExitGrid", MethodType.Getter)]
    public static class InSpaceNoOneCanHearYouRunAway
    {
        [HarmonyPostfix]
        public static void NoEscape(ExitMapGrid __instance, ref bool __result)
        {
            if (((Map)typeof(ExitMapGrid).GetField("map", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance)).Biome == ShipInteriorMod2.OuterSpaceBiome)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(CompSpawnerPawn), "TrySpawnPawn")]
    public static class SpaceCreaturesAreHungry
    {
        [HarmonyPostfix]
        public static void HungerLevel(ref Pawn pawn, bool __result)
        {
            var biome = pawn?.Map?.Biome;
            if (__result && biome != null && biome == ShipInteriorMod2.OuterSpaceBiome && pawn.needs?.food?.CurLevel != null)
                pawn.needs.food.CurLevel = 0.2f;
        }
    }

    [HarmonyPatch(typeof(TimedForcedExit))]
    [HarmonyPatch("StartForceExitAndRemoveMapCountdown", new Type[] { typeof(int) })]
    public static class ReEntryTime
    {
        [HarmonyPostfix]
        public static void SetTime(TimedForcedExit __instance)
        {
            if (__instance.parent.Biome != null && __instance.parent.Biome == ShipInteriorMod2.OuterSpaceBiome)
                typeof(TimedForcedExit).GetField("ticksLeftToForceExitAndRemoveMap", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(__instance, __instance.parent.GetComponent<TimeoutComp>().TicksLeft);
        }
    }

    [HarmonyPatch(typeof(TimedForcedExit), "ForceReform")]
    public static class ReEntryIsFatal
    {
        [HarmonyPrefix]
        public static bool BurnEmUp(MapParent mapParent)
        {
            if (mapParent.Biome != null && mapParent.Biome == ShipInteriorMod2.OuterSpaceBiome)
            {
                List<Pawn> deadPawns = new List<Pawn>();
                foreach (Thing t in mapParent.Map.spawnedThings)
                {
                    if (t is Pawn && ((Pawn)t).Faction == Faction.OfPlayer)
                        deadPawns.Add((Pawn)t);
                    t.Kill(new DamageInfo(DamageDefOf.Burn, 99999));
                }
                if (deadPawns.Count() > 0)
                {
                    string letterString = "LetterPawnsLostReEntry".Translate() + "\n\n";
                    foreach (Pawn deadPawn in deadPawns)
                        letterString += deadPawn.LabelShort + "\n";
                    Find.LetterStack.ReceiveLetter("LetterLabelPawnsLostReEntry".Translate(), letterString, LetterDefOf.NegativeEvent);
                }
                Find.WorldObjects.Remove(mapParent);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Trigger_UrgentlyHungry), "ActivateOn")]
    public static class MechsDontEat
    {
        static bool mechFound = false;
        [HarmonyPrefix]
        public static bool DisableMaybe(Lord lord)
        {
            mechFound = false;
            foreach (Pawn p in lord.ownedPawns)
            {
                if (p.RaceProps.IsMechanoid)
                {
                    mechFound = true;
                    return false;
                }
            }
            return true;
        }

        [HarmonyPostfix]
        public static void Okay(ref bool __result)
        {
            if (mechFound)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(Scenario))]
    [HarmonyPatch("Category", MethodType.Getter)]
    public static class FixThatBugInParticular
    {
        [HarmonyPrefix]
        public static bool NoLongerUndefined(Scenario __instance)
        {
            if (((ScenarioCategory)typeof(Scenario).GetField("categoryInt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance)) == ScenarioCategory.Undefined)
                typeof(Scenario).GetField("categoryInt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(__instance, ScenarioCategory.CustomLocal);
            return true;
        }
    }

    [HarmonyPatch(typeof(Faction), "RelationWith")]
    public static class FactionRelationsAcrossWorlds
    {
        static bool replaced;

        [HarmonyPrefix]
        public static bool RunOriginalMethod(Faction __instance, Faction other)
        {
            if (Current.ProgramState != ProgramState.Playing)
                return true;
            replaced = false;
            if (__instance == Faction.OfPlayer || other == Faction.OfPlayer)
                return true;
            if (WorldSwitchUtility.PastWorldTracker.WorldFactions.Keys.Contains(Find.World.info.name))
            {
                if (WorldSwitchUtility.PastWorldTracker.WorldFactions[Find.World.info.name].myFactions.Contains(__instance.GetUniqueLoadID()) && WorldSwitchUtility.PastWorldTracker.WorldFactions[Find.World.info.name].myFactions.Contains(other.GetUniqueLoadID()))
                    return true;
                replaced = true;
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        public static void ReturnDummy(ref FactionRelation __result)
        {
            if (replaced)
            {
                __result = new FactionRelation();
            }
        }
    }

    /*DEPRECATED - using vanilla techprint system now
    [HarmonyPatch(typeof(Designator_Build)), HarmonyPatch("Visible", MethodType.Getter)]
    public static class UnlockBuildings
    {
        [HarmonyPostfix]
        public static void Unlock(ref bool __result, Designator_Build __instance)
        {
            if (__instance.PlacingDef is ThingDef && ((ThingDef)__instance.PlacingDef).HasComp(typeof(CompSoSUnlock)))
            {
                if (WorldSwitchUtility.PastWorldTracker.Unlocks.Contains(((ThingDef)__instance.PlacingDef).GetCompProperties<CompProperties_SoSUnlock>().unlock) || DebugSettings.godMode)
                    __result = true;
                else
                    __result = false;
            }
        }
    }*/

    [HarmonyPatch(typeof(GameConditionManager), "ConditionIsActive")]
    public static class SpacecraftAreHardenedAgainstSolarFlares
    {
        [HarmonyPostfix]
        public static void Nope(ref bool __result, GameConditionManager __instance, GameConditionDef def)
        {
            if (def == GameConditionDefOf.SolarFlare && __instance.ownerMap != null && __instance.ownerMap.Biome != null && __instance.ownerMap.Biome == ShipInteriorMod2.OuterSpaceBiome)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(GameConditionManager))]
    [HarmonyPatch("ElectricityDisabled", MethodType.Getter)]
    public static class SpacecraftAreAlsoHardenedInOnePointOne
    {
        [HarmonyPostfix]
        public static void PowerOn(GameConditionManager __instance, ref bool __result)
        {
            if (__instance.ownerMap?.Biome == ShipInteriorMod2.OuterSpaceBiome)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(MapParent), "RecalculateHibernatableIncidentTargets")]
    public static class GiveMeRaidsPlease
    {
        [HarmonyPostfix]
        public static void RaidsAreFunISwear(MapParent __instance)
        {
            HashSet<IncidentTargetTagDef> hibernatableIncidentTargets = (HashSet<IncidentTargetTagDef>)typeof(MapParent).GetField("hibernatableIncidentTargets", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
            foreach (ThingWithComps current in __instance.Map.listerThings.ThingsOfDef(ThingDef.Named("JTDriveSalvage")).OfType<ThingWithComps>())
            {
                CompHibernatableSoS compHibernatable = current.TryGetComp<CompHibernatableSoS>();
                if (compHibernatable != null && compHibernatable.State == HibernatableStateDefOf.Starting && compHibernatable.Props.incidentTargetWhileStarting != null)
                {
                    if (hibernatableIncidentTargets == null)
                    {
                        hibernatableIncidentTargets = new HashSet<IncidentTargetTagDef>();
                    }
                    hibernatableIncidentTargets.Add(compHibernatable.Props.incidentTargetWhileStarting);
                }
            }
            typeof(MapParent).GetField("hibernatableIncidentTargets", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(__instance, hibernatableIncidentTargets);
        }
    }

    [HarmonyPatch(typeof(TileFinder), "TryFindNewSiteTile")]
    public static class NoQuestsNearTileZero
    {
        [HarmonyPrefix]
        public static bool DisableOriginalMethod()
        {
            return false;
        }

        [HarmonyPostfix]
        public static void CheckNonZeroTile(out int tile, int minDist, int maxDist, bool allowCaravans, bool preferCloserTiles, int nearThisTile, ref bool __result)
        {
            Func<int, int> findTile = delegate (int root)
            {
                int minDist2 = minDist;
                int maxDist2 = maxDist;
                Predicate<int> validator = (int x) => !Find.WorldObjects.AnyWorldObjectAt(x) && TileFinder.IsValidTileForNewSettlement(x, null);
                bool preferCloserTiles2 = preferCloserTiles;
                int result;
                if (TileFinder.TryFindPassableTileWithTraversalDistance(root, minDist2, maxDist2, out result, validator, false, preferCloserTiles2, false))
                {
                    return result;
                }
                return -1;
            };
            int arg;
            if (nearThisTile != -1)
            {
                arg = nearThisTile;
            }
            else if (!TileFinder.TryFindRandomPlayerTile(out arg, allowCaravans, (int x) => findTile(x) != -1 && (Find.World.worldObjects.MapParentAt(x) == null || !(Find.World.worldObjects.MapParentAt(x) is WorldObjectOrbitingShip))))
            {
                tile = -1;
                __result = false;
                return;
            }
            tile = findTile(arg);
            __result = (tile != -1);
        }
    }

    [HarmonyPatch(typeof(GlobalControls), "TemperatureString")]
    public static class ShowBreathability
    {
        [HarmonyPostfix]
        public static void CheckO2(ref string __result)
        {
            if (Find.CurrentMap.Biome == ShipInteriorMod2.OuterSpaceBiome)
            {
                IntVec3 intVec = UI.MouseCell();
                Room room = intVec.GetRoom(Find.CurrentMap, RegionType.Set_All);
                if (room == null)
                {
                    __result = __result + " (Vacuum)";
                }
                else if (!room.Role.defName.Equals("ShipInside") || room.OpenRoofCount > 0 || room.UsesOutdoorTemperature)
                {
                    bool inAirlock = false;
                    foreach (Thing t in Find.CurrentMap.thingGrid.ThingsAt(intVec))
                    {
                        if (t.def.defName.Equals("ShipAirlock"))
                            inAirlock = true;
                    }
                    if (inAirlock)
                    {
                        __result = __result + " (Breathable Atmosphere)";
                    }
                    else
                    {
                        __result = __result + " (Vacuum)";
                    }
                }
                else if (Find.CurrentMap.spawnedThings.Any(t => t.def.defName.Equals("Ship_LifeSupport") && ((ThingWithComps)t).GetComp<CompFlickable>().SwitchIsOn && ((ThingWithComps)t).GetComp<CompPowerTrader>().PowerOn))
                {
                    __result = __result + " (Breathable Atmosphere)";
                }
                else
                {
                    __result = __result + " (Non-Breathable Atmosphere)";
                }
            }
        }
    }

    [HarmonyPatch(typeof(ThingOwnerUtility), "GetAllThingsRecursively")]
    [HarmonyPatch(new Type[] { typeof(IThingHolder), typeof(bool) })]
    public static class FixThatPawnGenerationBug
    {
        [HarmonyPrefix]
        public static bool DisableMethod()
        {
            if (WorldSwitchUtility.SelectiveWorldGenFlag)
                return false;
            return true;
        }

        [HarmonyPostfix]
        public static void ReturnEmptyList(ref List<Thing> __result)
        {
            if (WorldSwitchUtility.SelectiveWorldGenFlag)
            {
                __result = new List<Thing>();
            }
        }
    }

    [HarmonyPatch(typeof(ThingOwnerUtility), "GetAllThingsRecursively")]
    [HarmonyPatch(new Type[] { typeof(IThingHolder), typeof(List<Thing>), typeof(bool), typeof(Predicate<IThingHolder>) })]
    public static class FixThatPawnGenerationBug2
    {
        [HarmonyPrefix]
        public static bool DisableMethod()
        {
            if (WorldSwitchUtility.SelectiveWorldGenFlag)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawnRelations")]
    public static class FixThatPawnGenerationBug3
    {
        [HarmonyPrefix]
        public static bool DisableMethod()
        {
            if (WorldSwitchUtility.SelectiveWorldGenFlag)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(PawnGroupMakerUtility), "TryGetRandomFactionForCombatPawnGroup")]
    public static class NoRaidsFromPreviousPlanets
    {
        [HarmonyPrefix]
        public static bool DisableMethod()
        {
            return false;
        }

        [HarmonyPostfix]
        public static void Replace(ref bool __result, float points, out Faction faction, Predicate<Faction> validator = null, bool allowNonHostileToPlayer = false, bool allowHidden = false, bool allowDefeated = false, bool allowNonHumanlike = true)
        {
            List<Faction> source = WorldSwitchUtility.FactionsOnCurrentWorld(Find.FactionManager.AllFactions).Where(delegate (Faction f)
            {
                int arg_E3_0;
                if ((allowHidden || !f.def.hidden) && (allowDefeated || !f.defeated) && (allowNonHumanlike || f.def.humanlikeFaction) && (allowNonHostileToPlayer || f.HostileTo(Faction.OfPlayer)) && f.def.pawnGroupMakers != null)
                {
                    if (f.def.pawnGroupMakers.Any((PawnGroupMaker x) => x.kindDef == PawnGroupKindDefOf.Combat) && (validator == null || validator(f)))
                    {
                        arg_E3_0 = ((points >= f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat)) ? 1 : 0);
                        return arg_E3_0 != 0;
                    }
                }
                arg_E3_0 = 0;
                return arg_E3_0 != 0;
            }).ToList<Faction>();
            __result = source.TryRandomElementByWeight((Faction f) => f.def.RaidCommonalityFromPoints(points), out faction);
        }
    }

    [HarmonyPatch(typeof(Building_CryptosleepCasket), "TryAcceptThing")]
    public static class UpdateCasketGraphicsA
    {
        [HarmonyPostfix]
        public static void UpdateIt(Building_CryptosleepCasket __instance)
        {
            if (__instance.Map != null && __instance.Spawned)
                __instance.Map.mapDrawer.MapMeshDirty(__instance.Position, MapMeshFlag.Buildings | MapMeshFlag.Things);
        }
    }

    [HarmonyPatch(typeof(Building_CryptosleepCasket), "EjectContents")]
    public static class UpdateCasketGraphicsB
    {
        [HarmonyPostfix]
        public static void UpdateIt(Building_CryptosleepCasket __instance)
        {
            if (__instance.Map != null && __instance.Spawned)
                __instance.Map.mapDrawer.MapMeshDirty(__instance.Position, MapMeshFlag.Buildings | MapMeshFlag.Things);
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "CanFireNowSub")]
    public static class NoTradersInSpace
    {
        [HarmonyPostfix]
        public static void Nope(IncidentParms parms, ref bool __result)
        {
            if (parms.target != null && parms.target is Map && ((Map)parms.target).Biome == ShipInteriorMod2.OuterSpaceBiome)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(RoofGrid), "GetCellExtraColor")]
    public static class ShowHullTilesOnRoofGrid
    {
        [HarmonyPostfix]
        public static void HullsAreColorful(RoofGrid __instance, int index, ref Color __result)
        {
            if (__instance.RoofAt(index).defName.Equals("RoofShip"))
                __result = Color.clear;
        }
    }

    [HarmonyPatch(typeof(TerrainGrid), "DoTerrainChangedEffects")]
    public static class RecreateShipTile
    {
        [HarmonyPostfix]
        public static void NoClearTilesPlease(TerrainGrid __instance, IntVec3 c)
        {
            Map map = (Map)typeof(TerrainGrid).GetField("map", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
            foreach (Thing t in map.thingGrid.ThingsAt(c))
            {
                if (t.TryGetComp<CompRoofMe>() != null)
                {
                    map.roofGrid.SetRoof(c, DefDatabase<RoofDef>.GetNamed("RoofShip"));
                    if (!map.terrainGrid.TerrainAt(c).layerable)
                        map.terrainGrid.SetTerrain(c, DefDatabase<TerrainDef>.GetNamed("FakeFloorInsideShip"));
                }
            }
        }
    }

    [HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "ShouldRemoveExistingFloorFirst")]
    public static class DontRemoveShipFloors
    {
        [HarmonyPostfix]
        public static void CheckShipFloor(Blueprint blue, ref bool __result)
        {
            if (blue.Map.terrainGrid.TerrainAt(blue.Position).defName.Equals("FakeFloorInsideShip"))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(QuestNode_GetMap), "IsAcceptableMap")]
    public static class NoQuestsInSpace
    {
        [HarmonyPostfix]
        public static void Fixpost(Map map, ref bool __result)
        {
            if (map.Parent != null && map.Biome == ShipInteriorMod2.OuterSpaceBiome)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(TickManager), "DoSingleTick")]
    public static class CombatTick
    {
        [HarmonyPostfix]
        public static void RunCombatManager()
        {
            if (ShipCombatManager.InCombat)
                ShipCombatManager.Tick();
        }
    }

    [HarmonyPatch(typeof(Building), "Destroy")]
    public static class NotifyCombatManager
    {
        [HarmonyPrefix]
        public static bool ShipPartIsDestroyed(Building __instance, DestroyMode mode)
        {
            if (!ShipCombatManager.InCombat || (mode != DestroyMode.KillFinalize && mode != DestroyMode.Deconstruct) || __instance is Frame)
                return true;
            if (__instance.Map == ShipCombatManager.EnemyShip)
                ShipCombatManager.EnemyShipDirty = true;
            else if (__instance.Map == ShipCombatManager.PlayerShip)
            {
                ShipCombatManager.PlayerShipDirty = true;
                if(mode!=DestroyMode.Deconstruct)
                    GenConstruct.PlaceBlueprintForBuild(__instance.def, __instance.Position, __instance.Map, __instance.Rotation, Faction.OfPlayer, __instance.Stuff);
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(LetterStack), "LettersOnGUI")]
    public static class ShipCombatOnGUI
    {
        [HarmonyPrefix]
        public static bool DrawShipRange(ref float baseY)
        {
            if (ShipCombatManager.InCombat)
            {
                Rect rect = new Rect(UI.screenWidth - 255, baseY - 40, 250, 40);
                Verse.Widgets.DrawMenuSection(rect);
                Verse.Widgets.DrawTexturePart(new Rect(UI.screenWidth - 233, baseY - 38, 225, 36), new Rect(0, 0, 1, 1), (Texture2D)ShipInteriorMod2.ruler.MatSingle.mainTexture);
                switch (ShipCombatManager.PlayerHeading)
                {
                    case -1:
                        Verse.Widgets.DrawTexturePart(new Rect(UI.screenWidth - 253, baseY - 38, 36, 36), new Rect(0, 0, 1, 1), (Texture2D)ShipInteriorMod2.shipOne.MatSingle.mainTexture);
                        break;
                    case 1:
                        Verse.Widgets.DrawTexturePart(new Rect(UI.screenWidth - 265, baseY - 38, 36, 36), new Rect(0, 0, -1, 1), (Texture2D)ShipInteriorMod2.shipOne.MatSingle.mainTexture);
                        break;
                    default:
                        Verse.Widgets.DrawTexturePart(new Rect(UI.screenWidth - 265, baseY - 38, 36, 36), new Rect(0, 0, -1, 1), (Texture2D)ShipInteriorMod2.shipZero.MatSingle.mainTexture);
                        break;
                }
                switch (ShipCombatManager.EnemyHeading)
                {
                    case -1:
                        Verse.Widgets.DrawTexturePart(new Rect(UI.screenWidth - 45 - ShipCombatManager.Range, baseY - 38, 36, 36), new Rect(0, 0, -1, 1), (Texture2D)ShipInteriorMod2.shipOne.MatSingle.mainTexture);
                        break;
                    case 1:
                        Verse.Widgets.DrawTexturePart(new Rect(UI.screenWidth - 29 - ShipCombatManager.Range, baseY - 38, 36, 36), new Rect(0, 0, 1, 1), (Texture2D)ShipInteriorMod2.shipOne.MatSingle.mainTexture);
                        break;
                    default:
                        Verse.Widgets.DrawTexturePart(new Rect(UI.screenWidth - 29 - ShipCombatManager.Range, baseY - 38, 36, 36), new Rect(0, 0, 1, 1), (Texture2D)ShipInteriorMod2.shipZero.MatSingle.mainTexture);
                        break;
                }
                foreach (ShipCombatProjectile proj in ShipCombatManager.Projectiles)
                {
                    if (proj.turret!=null && proj.turret.Map == ShipCombatManager.PlayerShip)
                    {
                        Verse.Widgets.DrawTexturePart(new Rect(UI.screenWidth - 53 - proj.range, baseY - 26, 12, 12), new Rect(0, 0, 1, 1), (Texture2D)ShipInteriorMod2.projectile.MatSingle.mainTexture);
                    }
                    else if(proj.turret!=null)
                    {
                        Verse.Widgets.DrawTexturePart(new Rect(UI.screenWidth - 245 + proj.range - ShipCombatManager.Range, baseY - 26, 12, 12), new Rect(0, 0, -1, 1), (Texture2D)ShipInteriorMod2.projectile.MatSingle.mainTexture);
                    }
                }
                if (Mouse.IsOver(rect))
                {
                    string iconTooltipText = "ShipCombatTooltip".Translate();
                    if (!iconTooltipText.NullOrEmpty())
                    {
                        TooltipHandler.TipRegion(rect, iconTooltipText);
                    }
                }
                baseY -= 50;
                if (ShipCombatManager.PlayerShipRoot == null || !ShipCombatManager.PlayerShipRoot.Spawned)
                    return true;
                Rect rect2 = new Rect(UI.screenWidth - 255, baseY - 40, 250, 40);
                Verse.Widgets.DrawMenuSection(rect2);
                PowerNet net = ShipCombatManager.PlayerShipRoot.GetComp<CompPower>().PowerNet;
                float capacity = 0;
                foreach (CompPowerBattery bat in net.batteryComps)
                    capacity += bat.Props.storedEnergyMax;
                Widgets.FillableBar(rect2.ContractedBy(6), net.CurrentStoredEnergy() / capacity, ShipInteriorMod2.PowerTex);
                Text.Font = GameFont.Small;
                Rect rect3 = rect2;
                rect3.y += 10;
                rect3.x = UI.screenWidth - 200;
                rect3.height = Text.LineHeight;
                Widgets.Label(rect3, "Energy: " + Mathf.Round(net.CurrentStoredEnergy()));
                baseY -= 50;
                Rect rect4 = new Rect(UI.screenWidth - 255, baseY - 40, 250, 40);
                Verse.Widgets.DrawMenuSection(rect4);
                ShipHeatNet net2 = ShipCombatManager.PlayerShipRoot.GetComp<CompShipHeat>().myNet;
                Widgets.FillableBar(rect4.ContractedBy(6), net2.StorageUsed / net2.StorageCapacity, ShipInteriorMod2.HeatTex);
                Rect rect5 = rect4;
                rect4.y += 10;
                rect4.x = UI.screenWidth - 200;
                rect4.height = Text.LineHeight;
                Widgets.Label(rect4, "Heat: " + Mathf.Round(net2.StorageUsed));
                baseY -= 50;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(BuildingProperties))]
    [HarmonyPatch("IsMortar", MethodType.Getter)]
    public static class TorpedoesCanBeLoaded
    {
        [HarmonyPostfix]
        public static void CheckThisOneToo(BuildingProperties __instance, ref bool __result)
        {
            if (__instance?.turretGunDef?.HasComp(typeof(CompChangeableProjectilePlural)) ?? false)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(ITab_Shells))]
    [HarmonyPatch("SelStoreSettingsParent", MethodType.Getter)]
    public static class TorpedoesHaveShellTab
    {
        [HarmonyPostfix]
        public static void CheckThisOneThree(ITab_Shells __instance, ref IStoreSettingsParent __result)
        {
            Building_ShipTurret building_TurretGun = Find.Selector.SingleSelectedObject as Building_ShipTurret;
            if (building_TurretGun != null)
            {
                __result = (IStoreSettingsParent)typeof(ITab_Storage).GetMethod("GetThingOrThingCompStoreSettingsParent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, new object[] { building_TurretGun.gun });
                return;
            }
        }
    }

    [HarmonyPatch(typeof(Room), "Notify_ContainedThingSpawnedOrDespawned")]
    public static class AirlockBugFix
    {
        [HarmonyPrefix]
        public static bool FixTheAirlockBug(Room __instance)
        {
            if (ShipInteriorMod2.AirlockBugFlag)
            {
                typeof(Room).GetField("statsAndRoleDirty", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(__instance, true);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn_Ownership))]
    [HarmonyPatch("OwnedRoom", MethodType.Getter)]
    public static class RoyaltyBedroomFix
    {
        [HarmonyPostfix]
        public static void ShipsCanBeRooms(Pawn_Ownership __instance, ref Room __result)
        {
            if (__result == null && __instance.OwnedBed != null && (__instance.OwnedBed.GetRoom()?.Role?.defName.Equals("ShipInside") ?? false))
            {
                __result = __instance.OwnedBed.GetRoom();
            }
        }
    }

    [HarmonyPatch(typeof(RCellFinder), "TryFindRandomExitSpot")]
    public static class NoPrisonBreaksInSpace
    {
        [HarmonyPostfix]
        public static void NoExits(Pawn pawn, ref bool __result)
        {
            if (pawn.Map.Biome == ShipInteriorMod2.OuterSpaceBiome)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(RoofCollapseCellsFinder), "ConnectsToRoofHolder")]
    public static class NoRoofCollapseInSpace
    {
        [HarmonyPostfix]
        public static void ZeroGee(ref bool __result, Map map)
        {
            if (map.Biome == ShipInteriorMod2.OuterSpaceBiome)
                __result = true;
        }
    }

    [HarmonyPatch(typeof(RoofCollapseUtility), "WithinRangeOfRoofHolder")]
    public static class NoRoofCollapseInSpace2
    {
        [HarmonyPostfix]
        public static void ZeroGee(ref bool __result, Map map)
        {
            if (map.Biome == ShipInteriorMod2.OuterSpaceBiome)
                __result = true;
        }
    }

    [HarmonyPatch(typeof(WorldGrid), "RawDataToTiles")]
    public static class FixWorldLoadBug
    {
        [HarmonyPrefix]
        public static bool SelectiveLoad()
        {
            return !WorldSwitchUtility.LoadWorldFlag;
        }
    }

    [HarmonyPatch(typeof(Projectile), "Launch")]
    [HarmonyPatch(new Type[] { typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(Thing), typeof(ThingDef) })]
    public static class TransferAmplifyBonus
    {
        [HarmonyPostfix]
        public static void OneMoreFactor(Projectile __instance, Thing equipment)
        {
            if(__instance is Projectile_ExplosiveShipCombat && equipment is Building_ShipTurret && ((Building_ShipTurret)equipment).AmplifierDamageBonus > 0)
            {
                typeof(Projectile).GetField("weaponDamageMultiplier", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(__instance, 1+((Building_ShipTurret)equipment).AmplifierDamageBonus);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_FilthTracker), "GainFilth", new Type[] { typeof(ThingDef), typeof(IEnumerable<string>) })]
    public static class RadioactiveAshIsRadioactive
    {
        [HarmonyPostfix]
        public static void OhNoISteppedInIt(ThingDef filthDef, Pawn_FilthTracker __instance)
        {
            if (filthDef.defName.Equals("Filth_SpaceReactorAsh"))
            {
                Pawn pawn = (Pawn)typeof(Pawn_FilthTracker).GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
                int damage = Rand.RangeInclusive(1, 2);
                pawn.TakeDamage(new DamageInfo(DamageDefOf.Burn, damage));
                float num = 0.025f;
                num *= pawn.GetStatValue(StatDefOf.ToxicSensitivity, true);
                if (num != 0f)
                {
                    HealthUtility.AdjustSeverity(pawn, HediffDefOf.ToxicBuildup, num);
                }
            }
        }
    }

    [HarmonyPatch(typeof(ColonistBarColonistDrawer), "DrawGroupFrame")]
    public static class ShipIconOnPawnBar
    {
        [HarmonyPostfix]
        public static void DrawShip(int group, ColonistBarColonistDrawer __instance)
        {
            List<ColonistBar.Entry> entries = Find.ColonistBar.Entries;
            bool foundColonist = false;
            foreach(ColonistBar.Entry entry in entries)
            {
                if (entry.group == group && (entry.pawn!=null || entry.map != ShipCombatManager.EnemyShip))
                    foundColonist = true;
            }
            if (!foundColonist)
            {
                Rect rect = (Rect)typeof(ColonistBarColonistDrawer).GetMethod("GroupFrameRect", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { group });
                Verse.Widgets.DrawTextureFitted(rect, ShipInteriorMod2.shipBar.MatSingle.mainTexture, 1);
            }
        }
    }

    [HarmonyPatch(typeof(SettleInExistingMapUtility), "SettleCommand")]
    public static class NoSpaceSettle
    {
        [HarmonyPostfix]
        public static void Nope(Command __result, Map map)
        {
            if(map.Biome==ShipInteriorMod2.OuterSpaceBiome)
            {
                __result.disabled = true;
                __result.disabledReason = "Cannot settle space sites";
            }
        }
    }

    [HarmonyPatch(typeof(ThingListGroupHelper), "Includes")]
    public static class ReactorsCanBeRefueled
    {
        [HarmonyPostfix]
        public static void CheckClass(ThingRequestGroup group, ThingDef def, ref bool __result)
        {
            if (group == ThingRequestGroup.Refuelable && def.HasComp(typeof(CompRefuelableOverdrivable)))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(Building), "ClaimableBy")]
    public static class NoClaimingEnemyShip
    {
        [HarmonyPostfix]
        public static void Nope(Building __instance, ref bool __result)
        {
            if (__instance.Map == ShipCombatManager.EnemyShip)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(TransferableUtility),"CanStack")]
    public static class MechsCannotStack
    {
        [HarmonyPrefix]
        public static bool Nope(Thing thing, ref bool __result)
        {
            if(thing is Pawn && ((Pawn)thing).RaceProps.IsMechanoid)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(GenSpawn), "SpawningWipes")]
    public static class ConduitWipe
    {
        [HarmonyPostfix]
        public static void PerhapsNoConduitHere(ref bool __result, BuildableDef newEntDef, BuildableDef oldEntDef)
        {
            if(oldEntDef.defName== "ShipHeatConduit")
            {
                ThingDef newDef = newEntDef as ThingDef;
                if (newDef == null)
                    return;
                foreach (CompProperties comp in newDef.comps)
                {
                    if (comp is CompProperties_ShipHeat)
                        __result = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(TravelingTransportPods))]
    [HarmonyPatch("TraveledPctStepPerTick",MethodType.Getter)]
    public static class InstantShuttleArrival
    {
        [HarmonyPostfix]
        public static void CloseRangeBoardingAction(TravelingTransportPods __instance, ref float __result)
        {
            if(ShipCombatManager.InCombat && (__instance.destinationTile==ShipCombatManager.PlayerShip.Tile || __instance.destinationTile==ShipCombatManager.EnemyShip.Tile))
            {
                __result = 1f;
            }
        }
    }

    //The following is for the now-cancelled holographic crew members
    /*[HarmonyPatch(typeof(Pawn_StoryTracker))]
    [HarmonyPatch("SkinColor", MethodType.Getter)]
    public static class HologramSkinColor
    {
        [HarmonyPostfix]
        public static void ImBlue(Pawn_StoryTracker __instance, ref Color __result)
        {
            if (__instance.childhood == ShipInteriorMod2.hologramBackstory)
                __result = new Color(0, 0.5f, 1, 0.4f);
        }
    }

    [HarmonyPatch(typeof(Pawn_StoryTracker), "ExposeData")]
    public static class HologramHairColor
    {
        [HarmonyPostfix]
        public static void Daboodeedaboodah(Pawn_StoryTracker __instance)
        {
            if (__instance.childhood == ShipInteriorMod2.hologramBackstory)
                __instance.hairColor = new Color(0, 0.5f, 1, 0.6f);
        }
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker))]
    [HarmonyPatch("PsychologicallyNude", MethodType.Getter)]
    public static class HologramNudityIsOkay
    {
        [HarmonyPostfix]
        public static void DoNotFearMyHolographicJunk(Pawn_ApparelTracker __instance, ref bool __result)
        {
            if (__instance.pawn.story.childhood == ShipInteriorMod2.hologramBackstory)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker), "WouldReplaceLockedApparel")]
    public static class HologramNudityIsMandatory
    {
        [HarmonyPostfix]
        public static void WitnessMyHolographicJunk(Pawn_ApparelTracker __instance, ref bool __result)
        {
            if (__instance.pawn.story.childhood == ShipInteriorMod2.hologramBackstory)
                __result = true;
        }
    }*/
}