﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using TractorMod.Framework;
using SFarmer = StardewValley.Farmer;

namespace TractorMod
{
    public class TractorMod : Mod
    {
        /*********
        ** Properties
        *********/
        private Vector2 tractorSpawnLocation = new Vector2(70, 13);
        private TractorConfig ModConfig { get; set; }
        private Tractor ATractor = null;
        private SaveCollection AllSaves;
        private bool IsNewDay = false;
        private bool IsNewTractor = false;
        const int buffUniqueID = 58012397;
        private bool TractorOn = false;
        private int mouseHoldDelay = 5;
        private int mouseHoldDelayCount;
        private Farm Farm = null;


        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            this.ModConfig = helper.ReadConfig<TractorConfig>();
            this.mouseHoldDelayCount = this.mouseHoldDelay;

            // spawn tractor & remove it before save
            TimeEvents.AfterDayStarted += this.TimeEvents_AfterDayStarted;
            LocationEvents.CurrentLocationChanged += this.LocationEvents_CurrentLocationChanged;
            SaveEvents.BeforeSave += this.SaveEvents_BeforeSave;

            // so that weird shit wouldnt happen
            MenuEvents.MenuChanged += this.MenuEvents_MenuChanged;
            MenuEvents.MenuClosed += this.MenuEvents_MenuClosed;

            // handle player interaction
            GameEvents.UpdateTick += this.UpdateTickEvent;
        }


        /*********
        ** Private methods
        *********/
        private void TimeEvents_AfterDayStarted(object sender, EventArgs eventArgs)
        {
            // set up for new day
            this.IsNewDay = true;
            this.IsNewTractor = true;
            this.Farm = Game1.getFarm();
        }

        private void LocationEvents_CurrentLocationChanged(object sender, EventArgsCurrentLocationChanged e)
        {
            // spawn tractor house & tractor
            if (this.IsNewDay && e.NewLocation == this.Farm)
            {
                this.LoadModInfo();
                this.IsNewDay = false;
            }
        }

        private void SaveEvents_BeforeSave(object sender, EventArgs eventArgs)
        {
            // save mod data
            this.SaveModInfo();

            // remove tractor from save
            foreach (TractorHouse tractorHouse in this.Farm.buildings.OfType<TractorHouse>().ToArray())
                this.Farm.destroyStructure(tractorHouse);
            foreach (GameLocation location in Game1.locations)
                this.RemoveEveryCharactersOfType<Tractor>(location);
        }

        private void MenuEvents_MenuChanged(object sender, EventArgsClickableMenuChanged e)
        {
            if (e.NewMenu is PhthaloBlueCarpenterMenu)
                PhthaloBlueCarpenterMenu.IsOpen = true;
            else
                PhthaloBlueCarpenterMenu.IsOpen = false;
        }

        private void MenuEvents_MenuClosed(object sender, EventArgsClickableMenuClosed e)
        {
            if (e.PriorMenu is PhthaloBlueCarpenterMenu)
            {
                ((PhthaloBlueCarpenterMenu)e.PriorMenu).Hangup();
                PhthaloBlueCarpenterMenu.IsOpen = false;
            }
        }

        private void UpdateTickEvent(object sender, EventArgs e)
        {
            if (this.ModConfig == null || Game1.currentLocation == null)
                return;

            MouseState currentMouseState = Mouse.GetState();
            KeyboardState currentKeyboardState = Keyboard.GetState();
            if (Keyboard.GetState().IsKeyDown(ModConfig.updateConfig))
                ModConfig = this.Helper.ReadConfig<TractorConfig>();
            DoAction(currentKeyboardState, currentMouseState);
        }

        private void SpawnTractor(bool SpawnAtFirstTractorHouse = true)
        {
            if (IsNewTractor == false)
                return;

            foreach (GameLocation GL in Game1.locations)
                RemoveEveryCharactersOfType<Tractor>(GL);

            if (SpawnAtFirstTractorHouse == false)
            {
                ATractor = new Tractor((int)tractorSpawnLocation.X, (int)tractorSpawnLocation.Y);
                ATractor.name = "Tractor";
                this.Farm.characters.Add((NPC)ATractor);
                Game1.warpCharacter((NPC)ATractor, "Farm", tractorSpawnLocation, false, true);
                IsNewTractor = false;
                return;
            }

            //spawn tractor
            foreach (Building building in this.Farm.buildings)
            {
                if (building is TractorHouse)
                {
                    if (building.daysOfConstructionLeft > 0)
                        continue;
                    ATractor = new Tractor((int)building.tileX + 1, (int)building.tileY + 1);
                    ATractor.name = "Tractor";
                    this.Farm.characters.Add((NPC)ATractor);
                    Game1.warpCharacter((NPC)ATractor, "Farm", new Vector2((int)building.tileX + 1, (int)building.tileY + 1), false, true);
                    IsNewTractor = false;
                    break;
                }
            }
            /*
            ATractor = new Tractor((int)tractorSpawnLocation.X, (int)tractorSpawnLocation.Y);
            ATractor.name = "Tractor";
            */
        }

        //use to write AllSaves info to some .json file to store save
        private void SaveModInfo()
        {
            if (AllSaves == null)
                AllSaves = new SaveCollection().Add(new Save(Game1.player.name, Game1.uniqueIDForThisGame));

            Save currentSave = AllSaves.FindSave(Game1.player.name, Game1.uniqueIDForThisGame);

            if (currentSave.SaveSeed != ulong.MaxValue)
            {
                currentSave.TractorHouse.Clear();
                foreach (Building b in this.Farm.buildings)
                {
                    if (b is TractorHouse)
                        currentSave.AddTractorHouse(b.tileX, b.tileY);
                }
            }
            else
            {
                AllSaves.saves.Add(new Save(Game1.player.name, Game1.uniqueIDForThisGame));
                SaveModInfo();
                return;
            }
            this.Helper.WriteJsonFile<SaveCollection>("TractorModSave.json", AllSaves);
        }

        //use to load save info from some .json file to AllSaves
        private void LoadModInfo()
        {
            this.AllSaves = this.Helper.ReadJsonFile<SaveCollection>("TractorModSave.json") ?? new SaveCollection();
            Save saveInfo = this.AllSaves.FindSave(Game1.player.name, Game1.uniqueIDForThisGame);
            if (saveInfo != null && saveInfo.SaveSeed != ulong.MaxValue)
            {
                foreach (Vector2 THS in saveInfo.TractorHouse)
                {
                    this.Farm.buildStructure(new TractorHouse().SetDaysOfConstructionLeft(0), THS, false, Game1.player);
                    if (IsNewTractor)
                        SpawnTractor();
                }
            }
        }

        private void RemoveEveryCharactersOfType<T>(GameLocation GL)
        {
            bool found = false;
            foreach (NPC character in GL.characters)
            {
                if (character is T)
                {
                    found = true;
                    GL.characters.Remove(character);
                    break;
                }
            }
            if (found)
                RemoveEveryCharactersOfType<T>(GL);
        }

        //execute most of the mod thinking here
        private void DoAction(KeyboardState currentKeyboardState, MouseState currentMouseState)
        {
            if (Game1.currentLocation == null)
                return;

            //use cellphone
            if (currentKeyboardState.IsKeyDown(ModConfig.PhoneKey))
            {
                if (Game1.activeClickableMenu != null)
                    return;
                if (PhthaloBlueCarpenterMenu.IsOpen)
                    return;
                Response[] answerChoices = new Response[2]
                {
                    new Response("Construct", "Browse PhthaloBlue Corp.'s building catalog"),
                    new Response("Leave", "Hang up")
                };

                Game1.currentLocation.createQuestionDialogue("Hello, this is PhthaloBlue Corporation. How can I help you?", answerChoices, this.OpenPhthaloBlueCarpenterMenu);
            }

            //summon Tractor
            if (currentKeyboardState.IsKeyDown(ModConfig.tractorKey))
            {
                Vector2 tile = Game1.player.getTileLocation();
                if (IsNewTractor) //check if you already own TractorHouse, if so then spawn tractor if its null
                {
                    Save currentSave = AllSaves.FindSave(Game1.player.name, Game1.uniqueIDForThisGame);
                    if (currentSave.TractorHouse.Count > 0)
                        SpawnTractor(false);

                }
                if (ATractor != null)
                    Game1.warpCharacter((NPC)ATractor, Game1.currentLocation.name, tile, false, true);
            }

            //summon Horse
            if (currentKeyboardState.IsKeyDown(ModConfig.horseKey))
            {
                foreach (GameLocation GL in Game1.locations)
                {
                    foreach (NPC character in GL.characters)
                    {
                        if (character is Tractor)
                        {
                            continue;
                        }
                        if (character is Horse)
                        {
                            Game1.warpCharacter((NPC)character, Game1.currentLocation.name, Game1.player.getTileLocation(), false, true);
                        }
                    }
                }
            }

            //disable Tractor Mode if player doesn't have TractorHouse built
            /*
            if (AllSaves.FindCurrentSave().TractorHouse.Count <= 0) 
                return;
            */

            //staring tractorMod
            TractorOn = false;
            switch (ModConfig.holdActivate)
            {
                default: break;
                case 1:
                    if (currentMouseState.LeftButton == ButtonState.Pressed)
                    {
                        if (mouseHoldDelayCount > 0)
                        {
                            mouseHoldDelayCount -= 1;
                        }
                        if (mouseHoldDelayCount <= 0)
                        {
                            TractorOn = true;
                            mouseHoldDelayCount = mouseHoldDelay;
                        }
                    }
                    break;
                case 2:
                    if (currentMouseState.RightButton == ButtonState.Pressed)
                    {
                        if (mouseHoldDelayCount > 0)
                        {
                            mouseHoldDelayCount -= 1;
                        }
                        if (mouseHoldDelayCount <= 0)
                        {
                            TractorOn = true;
                            mouseHoldDelayCount = mouseHoldDelay;
                        }
                    }
                    break;
                case 3:
                    if (currentMouseState.MiddleButton == ButtonState.Pressed)
                    {
                        if (mouseHoldDelayCount > 0)
                        {
                            mouseHoldDelayCount -= 1;
                        }
                        if (mouseHoldDelayCount <= 0)
                        {
                            TractorOn = true;
                            mouseHoldDelayCount = mouseHoldDelay;
                        }
                    }
                    break;
            }

            if (ATractor != null)
            {
                if (ATractor.rider == Game1.player)
                {
                    TractorOn = true;
                }
            }
            else //this should be unreachable code
            {
                SpawnTractor();
            }

            bool BuffAlready = false;
            if (TractorOn == false)
                return;

            //find if tractor buff already applied
            foreach (Buff buff in Game1.buffsDisplay.otherBuffs)
            {
                if (buff.which == buffUniqueID)
                {
                    if (buff.millisecondsDuration <= 35)
                    {
                        buff.millisecondsDuration = 1000;
                    }
                    BuffAlready = true;
                    break;
                }
            }

            //create new buff if its not already applied
            if (BuffAlready == false)
            {
                Buff tractorBuff = new Buff(0, 0, 0, 0, 0, 0, 0, 0, 0, ModConfig.tractorSpeed, 0, 0, 1, "Tractor Power", "Tractor Power");
                tractorBuff.which = buffUniqueID;
                tractorBuff.millisecondsDuration = 1000;
                Game1.buffsDisplay.addOtherBuff(tractorBuff);
                BuffAlready = true;
            }


            //if Tractor Mode (buff) is ON
            if (Game1.player.CurrentTool == null)
                ItemAction();
            else
                RunToolAction();
        }

        private void RunToolAction()
        {
            if (Game1.player.CurrentTool is MeleeWeapon && Game1.player.CurrentTool.name.ToLower().Contains("scythe"))
                HarvestAction();
            else
                ToolAction();
        }

        private void ItemAction()
        {
            if (Game1.player.CurrentItem == null)
                return;

            if (Game1.player.CurrentItem.getCategoryName().ToLower() == "seed" || Game1.player.CurrentItem.getCategoryName().ToLower() == "fertilizer")
            {
                List<Vector2> affectedTileGrid = MakeVector2TileGrid(Game1.player.getTileLocation(), ModConfig.ItemRadius);

                foreach (Vector2 tile in affectedTileGrid)
                {
                    TerrainFeature terrainTile;
                    if (Game1.currentLocation.terrainFeatures.TryGetValue(tile, out terrainTile))
                    {
                        if (Game1.currentLocation.terrainFeatures[tile] is HoeDirt)
                        {
                            HoeDirt hoedirtTile = (HoeDirt)Game1.currentLocation.terrainFeatures[tile];

                            if (Game1.player.CurrentItem.getCategoryName().ToLower() == "seed")
                            {
                                if (hoedirtTile.crop != null)
                                    continue;
                                if (hoedirtTile.plant(Game1.player.CurrentItem.parentSheetIndex, (int)tile.X, (int)tile.Y, Game1.player))
                                {
                                    Game1.player.CurrentItem.Stack -= 1;
                                    if (Game1.player.CurrentItem.Stack <= 0)
                                    {
                                        Game1.player.removeItemFromInventory(Game1.player.CurrentItem);
                                        return;
                                    }
                                }
                            }
                            if (Game1.player.CurrentItem.getCategoryName().ToLower() == "fertilizer")
                            {
                                if (hoedirtTile.fertilizer != 0)
                                    continue;
                                hoedirtTile.fertilizer = Game1.player.CurrentItem.parentSheetIndex;
                                Game1.player.CurrentItem.Stack -= 1;
                                if (Game1.player.CurrentItem.Stack <= 0)
                                {
                                    Game1.player.removeItemFromInventory(Game1.player.CurrentItem);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void HarvestAction()
        {
            //check if tool is enable from config
            ToolConfig ConfigForCurrentTool = new ToolConfig("");
            foreach (ToolConfig TC in ModConfig.tool)
            {
                if (Game1.player.CurrentTool.name.Contains("Scythe"))
                {
                    ConfigForCurrentTool = TC;
                    break;
                }
            }
            if (ConfigForCurrentTool.name == "")
                return;
            int effectRadius = ConfigForCurrentTool.effectRadius;
            List<Vector2> affectedTileGrid = MakeVector2TileGrid(Game1.player.getTileLocation(), effectRadius);

            //harvesting objects
            foreach (Vector2 tile in affectedTileGrid)
            {
                StardewValley.Object anObject;
                if (Game1.currentLocation.objects.TryGetValue(tile, out anObject))
                {
                    if (anObject.isSpawnedObject)
                    {
                        if (anObject.isForage(Game1.currentLocation))
                        {
                            bool gatherer = CheckFarmerProfession(Game1.player, SFarmer.gatherer);
                            bool botanist = CheckFarmerProfession(Game1.player, SFarmer.botanist);
                            if (botanist)
                                anObject.quality = 4;
                            if (gatherer)
                            {
                                int num = new Random().Next(0, 100);
                                if (num < 20)
                                {
                                    anObject.stack *= 2;
                                }
                            }
                        }

                        for (int i = 0; i < anObject.stack; i++)
                            Game1.currentLocation.debris.Add(new Debris(anObject, new Vector2(tile.X * Game1.tileSize, tile.Y * Game1.tileSize)));
                        Game1.currentLocation.removeObject(tile, false);
                        continue;
                    }

                    if (anObject.name.ToLower().Contains("weed"))
                    {
                        Game1.createObjectDebris(771, (int)tile.X, (int)tile.Y, -1, 0, 1f, Game1.currentLocation); //fiber
                        if (new Random().Next(0, 10) < 1) //10% mixed seeds
                            Game1.createObjectDebris(770, (int)tile.X, (int)tile.Y, -1, 0, 1f, Game1.currentLocation); //fiber
                        Game1.currentLocation.removeObject(tile, false);
                        continue;
                    }
                }
            }

            //harvesting plants
            foreach (Vector2 tile in affectedTileGrid)
            {
                TerrainFeature terrainTile;
                if (Game1.currentLocation.terrainFeatures.TryGetValue(tile, out terrainTile))
                {
                    if (Game1.currentLocation.terrainFeatures[tile] is HoeDirt)
                    {
                        HoeDirt hoedirtTile = (HoeDirt)Game1.currentLocation.terrainFeatures[tile];
                        if (hoedirtTile.crop == null)
                            continue;

                        int harvestMethod = hoedirtTile.crop.harvestMethod;
                        hoedirtTile.crop.harvestMethod = Crop.sickleHarvest;

                        if (hoedirtTile.crop.whichForageCrop == 1) //spring onion
                        {
                            StardewValley.Object anObject = new StardewValley.Object(399, 1);
                            bool gatherer = CheckFarmerProfession(Game1.player, SFarmer.gatherer);
                            bool botanist = CheckFarmerProfession(Game1.player, SFarmer.botanist);
                            if (botanist)
                            {
                                anObject.quality = 4;
                            }
                            if (gatherer)
                            {
                                int num = new Random().Next(0, 100);
                                if (num < 20)
                                {
                                    anObject.stack *= 2;
                                }
                            }
                            for (int i = 0; i < anObject.stack; i++)
                                Game1.currentLocation.debris.Add(new Debris(anObject, new Vector2(tile.X * Game1.tileSize, tile.Y * Game1.tileSize)));

                            hoedirtTile.destroyCrop(tile, true);
                            continue;
                        }

                        if (hoedirtTile.crop.harvest((int)tile.X, (int)tile.Y, hoedirtTile))
                        {
                            if (hoedirtTile.crop.indexOfHarvest == 421) //sun flower
                            {
                                int seedDrop = new Random().Next(1, 4);
                                for (int i = 0; i < seedDrop; i++)
                                    Game1.createObjectDebris(431, (int)tile.X, (int)tile.Y, -1, 0, 1f, Game1.currentLocation); //spawn sunflower seeds
                            }

                            if (hoedirtTile.crop.regrowAfterHarvest == -1)
                            {
                                hoedirtTile.destroyCrop(tile, true);
                            }
                        }

                        if (hoedirtTile.crop != null)
                            hoedirtTile.crop.harvestMethod = harvestMethod;
                        continue;
                    }

                    if (Game1.currentLocation.terrainFeatures[tile] is FruitTree)
                    {
                        FruitTree tree = (FruitTree)Game1.currentLocation.terrainFeatures[tile];
                        tree.shake(tile, false);
                        continue;
                    }

                    //will test once I have giantcrop
                    /*
                    if(Game1.currentLocation.terrainFeatures[tile] is GiantCrop)
                    {
                        GiantCrop bigCrop = (GiantCrop)Game1.currentLocation.terrainFeatures[tile];
                        bigCrop.performToolAction((Tool)new Axe(), 100, tile);
                        continue;
                    }
                    */

                    if (Game1.currentLocation.terrainFeatures[tile] is Grass)
                    {
                        Grass grass = (Grass)Game1.currentLocation.terrainFeatures[tile];
                        grass = null;
                        Game1.currentLocation.terrainFeatures.Remove(tile);
                        Farm.tryToAddHay(2);
                        continue;
                    }


                    if (Game1.currentLocation.terrainFeatures[tile] is Tree)
                    {
                        continue;
                    }
                }
            }
        }

        private void ToolAction()
        {
            Tool currentTool = Game1.player.CurrentTool;

            //check if tool is enable from config
            ToolConfig ConfigForCurrentTool = new ToolConfig("");
            foreach (ToolConfig TC in ModConfig.tool)
            {
                if (currentTool.name.Contains(TC.name))
                {
                    ConfigForCurrentTool = TC;
                    break;
                }
            }

            if (ConfigForCurrentTool.name == "")
                return;
            else
                if (currentTool.upgradeLevel < ConfigForCurrentTool.minLevel)
                return;

            if (ConfigForCurrentTool.activeEveryTickAmount > 1)
            {
                ConfigForCurrentTool.incrementActiveCount();
                if (ConfigForCurrentTool.canToolBeActive() == false)
                    return;
            }

            int effectRadius = ConfigForCurrentTool.effectRadius;
            int currentWater = 0;
            if (currentTool is WateringCan)
            {
                WateringCan currentWaterCan = (WateringCan)currentTool;
                currentWater = currentWaterCan.WaterLeft;
            }

            float currentStamina = Game1.player.stamina;
            List<Vector2> affectedTileGrid = MakeVector2TileGrid(Game1.player.getTileLocation(), effectRadius);

            //if player on horse
            Vector2 currentMountPosition = new Vector2();
            if (Game1.player.isRidingHorse())
            {
                currentMountPosition = Game1.player.getMount().position;
                Game1.player.getMount().position = new Vector2(0, 0);
            }

            //Tool 

            //before tool use
            int toolUpgrade = currentTool.upgradeLevel;
            currentTool.upgradeLevel = 4;
            Game1.player.toolPower = 0;

            //tool use
            foreach (Vector2 tile in affectedTileGrid)
            {
                currentTool.DoFunction(Game1.currentLocation, (int)(tile.X * Game1.tileSize), (int)(tile.Y * Game1.tileSize), 0, Game1.player);
            }

            //after tool use
            if (Game1.player.isRidingHorse())
                Game1.player.getMount().position = currentMountPosition;
            currentTool.upgradeLevel = toolUpgrade;
            Game1.player.stamina = currentStamina;

            if (currentTool is WateringCan)
            {
                WateringCan currentWaterCan = (WateringCan)currentTool;
                currentWaterCan.WaterLeft = currentWater;
            }
        }

        //this will make a list of all the vector2 around origin with size radius (ex: size = 3 => 7x7 grid)
        private List<Vector2> MakeVector2GridForHorse(Vector2 origin, int size)
        {
            List<Vector2> grid = new List<Vector2>();
            if (Game1.player.movementDirections.Count <= 0)
                return new List<Vector2>();

            for (int i = 0; i < 2 * size + 1; i++)
            {
                for (int j = 0; j < 2 * size + 1; j++)
                {
                    bool NoAdd = false;
                    switch (Game1.player.movementDirections[0])
                    {
                        case 0: if (j != 0) NoAdd = true; break;
                        case 2: if (j != 0) NoAdd = true; break;

                        case 1: if (i != 0) NoAdd = true; break;
                        case 3: if (i != 0) NoAdd = true; break;
                    }
                    if (NoAdd)
                        continue;

                    Vector2 newVec = new Vector2(origin.X - size * Game1.tileSize, origin.Y - size * Game1.tileSize);
                    newVec.X += (float)i * Game1.tileSize;
                    newVec.Y += (float)j * Game1.tileSize;
                    grid.Add(newVec);
                }
            }

            //adjust depending on facing
            for (int i = 0; i < grid.Count; i++)
            {
                Vector2 temp = grid[i];
                int numberOfTileBehindPlayer = 1;
                switch (Game1.player.movementDirections[0])
                {
                    case 0: temp.Y += (numberOfTileBehindPlayer + 2) * Game1.tileSize; break; //go up
                    case 1: temp.X -= numberOfTileBehindPlayer * Game1.tileSize; break; //right
                    case 2: temp.Y -= (numberOfTileBehindPlayer) * Game1.tileSize; break; //down
                    case 3: temp.X += (numberOfTileBehindPlayer + 2) * Game1.tileSize; break; //left
                }
                grid[i] = temp;
            }
            return grid;
        }

        private List<Vector2> MakeVector2Grid(Vector2 origin, int size)
        {
            List<Vector2> grid = new List<Vector2>();

            for (int i = 0; i < 2 * size + 1; i++)
            {
                for (int j = 0; j < 2 * size + 1; j++)
                {
                    Vector2 newVec = new Vector2(origin.X - size * Game1.tileSize,
                                                origin.Y - size * Game1.tileSize);

                    newVec.X += (float)i * Game1.tileSize;
                    newVec.Y += (float)j * Game1.tileSize;

                    grid.Add(newVec);
                }
            }
            return grid;
        }

        /*
        private List<Vector2> MakeVector2TileGridForHorse(Vector2 origin, int size)
        {
            List<Vector2> grid = new List<Vector2>();
            if (Game1.player.isMoving() == false)
                return new List<Vector2>();
            
            for (int i = 0; i < 2 * size + 1; i++)
            {
                for (int j = 0; j < 2 * size + 1; j++)
                {
                    bool NoAdd = false;
                    switch (playerOrientation)
                    {
                        default: break;
                        case 0: if (j != 0) NoAdd = true; break;
                        case 2: if (j != 0) NoAdd = true; break;
                        case 1: if (i != 0) NoAdd = true; break;
                        case 3: if (i != 0) NoAdd = true; break;
                    }
                    if (NoAdd)
                        continue;

                    Vector2 newVec = new Vector2(origin.X - size, origin.Y - size);
                    newVec.X += (float)i;
                    newVec.Y += (float)j;
                    grid.Add(newVec);
                }
            }

            //adjust depending on facing
            for (int i = 0; i < grid.Count; i++)
            {
                Vector2 temp = grid[i];
                int numberOfTileBehindPlayer = 1;
                switch (playerOrientation)
                {
                    default: break;
                    case 0: temp.Y += (numberOfTileBehindPlayer + 2); break; //go up
                    case 1: temp.X -= numberOfTileBehindPlayer; break; //right
                    case 2: temp.Y -= (numberOfTileBehindPlayer); break; //down
                    case 3: temp.X += (numberOfTileBehindPlayer + 2); break; //left
                }
                grid[i] = temp;
            }
            return grid;
        }
        */

        private List<Vector2> MakeVector2TileGrid(Vector2 origin, int size)
        {
            List<Vector2> grid = new List<Vector2>();
            for (int i = 0; i < 2 * size + 1; i++)
            {
                for (int j = 0; j < 2 * size + 1; j++)
                {
                    Vector2 newVec = new Vector2(origin.X - size,
                                                origin.Y - size);

                    newVec.X += (float)i;
                    newVec.Y += (float)j;

                    grid.Add(newVec);
                }
            }

            return grid;
        }

        private int FindEmptySlotInFarmerInventory(SFarmer input)
        {
            for (int i = 0; i < input.items.Count; i++)
            {
                if (input.items[i] == null)
                    return i;
            }
            return -1;
        }

        private int FindSlotWithSameItemInFarmerInventory(SFarmer input, Item inputItem)
        {
            for (int i = 0; i < input.items.Count; i++)
            {
                if (input.items[i] == null)
                    continue;
                if (input.items[i].getRemainingStackSpace() <= 0)
                    continue;
                if (input.items[i].canStackWith(inputItem))
                {
                    return i;
                }
            }
            return -1;
        }

        private int FindSlotForInputItemInFarmerInventory(SFarmer input, Item inputItem)
        {
            int slot = FindSlotWithSameItemInFarmerInventory(input, inputItem);
            if (slot == -1)
            {
                slot = FindEmptySlotInFarmerInventory(input);
            }
            return slot;
        }

        private bool CheckFarmerProfession(SFarmer farmerInput, int professionIndex)
        {
            foreach (int i in farmerInput.professions)
            {
                if (i == professionIndex)
                    return true;
            }
            return false;
        }

        private List<Vector2> RemoveTileWithResourceClumpType(int ResourceClumpIndex, List<Vector2> input)
        {
            List<Vector2> output = new List<Vector2>();
            foreach (Vector2 tile in input)
            {
                if (CheckIfTileBelongToResourceClump(ResourceClumpIndex, tile))
                    continue;
                else
                    output.Add(new Vector2(tile.X, tile.Y));
            }

            return output;
        }

        private bool CheckIfTileBelongToResourceClump(int ResourceClumpIndex, Vector2 tile)
        {
            TerrainFeature check;
            if (Game1.currentLocation.terrainFeatures.TryGetValue(tile, out check))
            {
                if (check is ResourceClump)
                {
                    ResourceClump RC = (ResourceClump)check;
                    if (RC.parentSheetIndex == ResourceClumpIndex)
                        return true;
                }
            }

            Vector2 tileToLeft = new Vector2(tile.X - 1, tile.Y);
            if (Game1.currentLocation.terrainFeatures.TryGetValue(tileToLeft, out check))
            {
                if (check is ResourceClump)
                {
                    ResourceClump RC = (ResourceClump)check;
                    if (RC.parentSheetIndex == ResourceClumpIndex)
                        return true;
                }
            }

            Vector2 tileToTopLeft = new Vector2(tile.X - 1, tile.Y - 1);
            if (Game1.currentLocation.terrainFeatures.TryGetValue(tileToTopLeft, out check))
            {
                if (check is ResourceClump)
                {
                    ResourceClump RC = (ResourceClump)check;
                    if (RC.parentSheetIndex == ResourceClumpIndex)
                        return true;
                }
            }

            Vector2 tileToTop = new Vector2(tile.X, tile.Y - 1);
            if (Game1.currentLocation.terrainFeatures.TryGetValue(tileToTop, out check))
            {
                if (check is ResourceClump)
                {
                    ResourceClump RC = (ResourceClump)check;
                    if (RC.parentSheetIndex == ResourceClumpIndex)
                        return true;
                }
            }

            return false;
        }

        private void OpenPhthaloBlueCarpenterMenu(SFarmer who, string whichAnswer)
        {
            switch (whichAnswer)
            {
                case "Construct":
                    BluePrint TractorBP = new BluePrint("Garage");
                    TractorBP.itemsRequired.Clear();
                    TractorBP.texture = Game1.content.Load<Texture2D>("..\\Mods\\TractorMod\\assets\\TractorHouse");
                    TractorBP.humanDoor = new Point(-1, -1);
                    TractorBP.animalDoor = new Point(-2, -1);
                    TractorBP.mapToWarpTo = "null";
                    TractorBP.description = "A structure to store PhthaloBlue Corp.'s tractor.\nTractor included!";
                    TractorBP.blueprintType = "Buildings";
                    TractorBP.nameOfBuildingToUpgrade = "";
                    TractorBP.actionBehavior = "null";
                    TractorBP.maxOccupants = -1;
                    TractorBP.moneyRequired = ModConfig.TractorHousePrice;
                    TractorBP.tilesWidth = 4;
                    TractorBP.tilesHeight = 2;
                    TractorBP.sourceRectForMenuView = new Rectangle(0, 0, 64, 96);
                    TractorBP.namesOfOkayBuildingLocations.Clear();
                    TractorBP.namesOfOkayBuildingLocations.Add("Farm");
                    TractorBP.magical = true;
                    Game1.activeClickableMenu = new PhthaloBlueCarpenterMenu(this.Monitor).AddBluePrint<TractorHouse>(TractorBP);
                    ((PhthaloBlueCarpenterMenu)Game1.activeClickableMenu).WherePlayerOpenThisMenu = Game1.currentLocation;
                    break;
                case "Leave":
                    new PhthaloBlueCarpenterMenu(this.Monitor).Hangup();
                    break;
            }
        }
    }
}
