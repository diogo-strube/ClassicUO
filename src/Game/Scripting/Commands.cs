#region license

// Copyright (C) 2020 ClassicUO Development Community on Github
// 
// This project is an alternative client for the game Ultima Online.
// The goal of this is to develop a lightweight client considering
// new technologies.
// 
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
// 
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Network;

namespace ClassicUO.Game.Scripting
{
    // Class grouping all command related functionality, including implemented handles
    public static class Commands
    {
        // Helper function to register command execution
        internal static void AddHandler(string usage, Command.Handler execLogic, int waitTime = 200, CommandGroup group = CommandGroup.None)
        {
            var cmd = new Command(usage, execLogic, waitTime, group);
            Interpreter.RegisterCommandHandler(cmd.Keyword, cmd.Execute);
        }

        // Helper function to invoke another command
        internal static bool Invoke(Command.Handler cmd, bool quiet, bool force, params (string Name, string Value)[] args)
        {
            // We build an argument list with virtual args
            VirtualArgument[] virtualArgs = new VirtualArgument[args.Length];
            string[] definitions = new string[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                definitions[i] = args[i].Name;
                virtualArgs[i] = new VirtualArgument(args[i].Value);
            }
            ArgumentList argList = new ArgumentList(virtualArgs, args.Length, definitions);
            // and we return the command result
            return cmd(argList, quiet, force);
        }

        // ATTENTION: This debugging appoach may be slow. Use only for QA and disable by default.
        internal static bool DebugCommands = true;
        internal static void DebugMsg(string msg)
        {
            if(DebugCommands)
            {
                StackFrame stack = new StackFrame(1, false);
                GameActions.Print($"{stack.GetMethod().Name}: {msg}", hue: 0x104, type: MessageType.System);
            }
        }

        // Agent related storages (to be moved to Profile)?
        // Profiles are very similar to lists, but instead of string containg serials
        // Commands such as Organizer and Dress use profiles
        static public Dictionary<string, List<uint>> Profiles = new Dictionary<string, List<uint>>();

        public static void Register()
        {
            // UOSTREAM: Add value mapping (similar to aliases) to specific arguments as handled by UO Steam
            ArgumentList.AddMap("color", "any", ushort.MaxValue);
            ArgumentList.AddMap("color", (ushort)0, ushort.MaxValue);
            ArgumentList.AddMap("direction", "southeast", "down");
            ArgumentList.AddMap("direction", "southwest", "left");
            ArgumentList.AddMap("direction", "northeast", "right");
            ArgumentList.AddMap("direction", "northwest", "up");

            // Adding default profiles
            Profiles.Add("dressconfig-temporary", new List<uint>());
            Profiles.Add("temporary-generic", new List<uint>());

            // Add definitions for all supported commands
            AddHandler("setability ('primary'/'secondary'/'stun'/'disarm') ['on'/'off']", SetAbility);
            AddHandler("attack (serial)", Attack);
            AddHandler("clearhands ('left'/'right'/'both')", ClearHands, 800, CommandGroup.PickUp);
            AddHandler("clickobject (serial)", ClickObject);
            AddHandler("bandageself", BandageSelf);
            AddHandler("usetype (graphic) [color] [source] [range or search level]", UseType);
            AddHandler("useobject (serial)", UseObject);
            AddHandler("useonce (graphic) [color]", UseOnce);
            AddHandler("moveitem (serial) (destination) [(x, y, z)] [amount]", MoveItem, 800, CommandGroup.PickUp);
            AddHandler("moveitemoffset (serial) (destination) [(x, y, z)] [amount]", MoveItemOffset, 800, CommandGroup.PickUp);
            AddHandler("movetype (graphic) (source) (destination) [(x, y, z)] [color] [amount] [range or search level]", MoveType, 800, CommandGroup.PickUp);
            AddHandler("movetypeoffset (graphic) (source) 'ground' [(x, y, z)] [color] [amount] [range or search level]", MoveTypeOffset, 800, CommandGroup.PickUp);
            AddHandler("walk (direction)", MovementLogic(false), MovementSpeed.STEP_DELAY_WALK);
            AddHandler("turn (direction)", MovementLogic(false), MovementSpeed.STEP_DELAY_WALK);
            AddHandler("run (direction)", MovementLogic(true), MovementSpeed.STEP_DELAY_WALK / 2);
            AddHandler("useskill ('skill name'/'last')", UseSkill);
            AddHandler("feed (serial) ('food name'/'food group'/'any'/graphic) [color] [amount]", Feed);
            AddHandler("rename (serial) ('name')", Rename);
            AddHandler("shownames ['mobiles'/'corpses']", ShowNames);
            AddHandler("togglehands ('left'/'right')", ToggleHands, 800, CommandGroup.PickUp);
            AddHandler("equipitem (serial) [layer]", EquipItem, 800, CommandGroup.PickUp);
            AddHandler("togglemounted", ToggleMounted, 800);
            //AddDefinition("equipwand ('spell name'/'any'/'undefined') [minimum charges]", EquipWand, WaitForMs(500), Command.Attributes.ComplexInterAction);
            //AddDefinition("buy ('list name')", Buy, WaitForMs(500), Command.Attributes.ComplexInterAction);
            //AddDefinition("sell ('list name')", Sell, WaitForMs(500), Command.Attributes.ComplexInterAction);
            //AddDefinition("clearbuy", ClearBuy, WaitForMs(500), Command.Attributes.ComplexInterAction);
            //AddDefinition("clearsell", ClearSell, WaitForMs(500), Command.Attributes.ComplexInterAction);
            //AddDefinition("organizer ('profile name') [source] [destination]", Organizer, WaitForMs(500), Command.Attributes.ComplexInterAction);
            //AddDefinition("organizing", Organizing, WaitForMs(500), Command.Attributes.ComplexInterAction);
            AddHandler("autoloot", UnsupportedCmd);
            AddHandler("dress ['profile name']", Dress);
            AddHandler("undress ['profile name']", Undress);
            AddHandler("dressconfig", Dressconfig);
            AddHandler("toggleautoloot", UnsupportedCmd);
            //AddDefinition("togglescavenger", UnsupportedCmd, WaitForMs(25));
            AddHandler("clickscreen (x) (y) ['single'/'double'] ['left'/'right']", ClickScreen);
            AddHandler("findtype (graphic) [color] [source] [amount] [range or search level]", FindType);
            AddHandler("findobject (serial) [color] [source] [amount] [range]", FindObject);
            AddHandler("poplist ('list name') ('element value'/'front'/'back')", PopList);
            AddHandler("pushlist ('list name') ('element value') ['front'/'back']", PushList);
            AddHandler("createlist ('list name')", CreateList);
            AddHandler("removelist ('list name')", RemoveList);
            AddHandler("msg ('text') [color]", Msg);
            AddHandler("setalias ('alias name') [serial]", SetAlias);
            AddHandler("unsetalias ('alias name')", UnsetAlias);
            AddHandler("promptalias ('alias name')", PromptAlias);
            AddHandler("findalias ('alias name')", FindAlias);
            AddHandler("waitforgump (gump id/'any') (timeout)", WaitForGump);
            AddHandler("waitfortarget (timeout)", WaitForTarget);
            AddHandler("pause (timeout)", Pause);
            AddHandler("target (serial)", ClickTarget);
            ////Interpreter.RegisterCommandHandler("poplist", );
            //Interpreter.RegisterCommandHandler("pushlist", );
            ////Interpreter.RegisterCommandHandler("removelist", );
            ////Interpreter.RegisterCommandHandler("createlist", CreateList);

            //Interpreter.RegisterCommandHandler("findtype", FindType);
            //Interpreter.RegisterCommandHandler("fly", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("land", UnimplementedCommand);

            //Interpreter.RegisterCommandHandler("useobject", UseObject);
            //Interpreter.RegisterCommandHandler("moveitem", MoveItem);






            //Interpreter.RegisterExpressionHandler("findobject", ExpFindObject);

            //#region Deprecated (but supported)
            //Interpreter.RegisterCommandHandler("clearuseonce", Deprecated);
            //#endregion

            //#region Deprecated (not supported)
            //Interpreter.RegisterCommandHandler("autoloot", Deprecated);
            //Interpreter.RegisterCommandHandler("toggleautoloot", Deprecated);
            //#endregion



            //Interpreter.RegisterCommandHandler("msg", Msg);

            //Interpreter.RegisterCommandHandler("pause", Pause);


            ////Interpreter.RegisterCommandHandler("togglescavenger", ToggleScavenger);

            ////Interpreter.RegisterCommandHandler("promptalias", PromptAlias);
            ////Interpreter.RegisterCommandHandler("waitforgump", WaitForGump);
            ////Interpreter.RegisterCommandHandler("clearjournal", ClearJournal);
            ////Interpreter.RegisterCommandHandler("waitforjournal", WaitForJournal);

            ////Interpreter.RegisterCommandHandler("clearlist", ClearList);
            ////Interpreter.RegisterCommandHandler("ping", Ping);
            ////Interpreter.RegisterCommandHandler("resync", Resync);
            ////Interpreter.RegisterCommandHandler("messagebox", MessageBox);
            ////Interpreter.RegisterCommandHandler("paperdoll", Paperdoll);
            ////Interpreter.RegisterCommandHandler("headmsg", HeadMsg);
            ////Interpreter.RegisterCommandHandler("sysmsg", SysMsg);
            ////Interpreter.RegisterCommandHandler("cast", Cast);
            ////Interpreter.RegisterCommandHandler("waitfortarget", WaitForTarget);
            ////Interpreter.RegisterCommandHandler("canceltarget", CancelTarget);
            ////Interpreter.RegisterCommandHandler("target", Target);
            ////Interpreter.RegisterCommandHandler("targettype", TargetType);
            ////Interpreter.RegisterCommandHandler("targetground", TargetGround);
            ////Interpreter.RegisterCommandHandler("targettile", TargetTile);
            ////Interpreter.RegisterCommandHandler("targettileoffset", TargetTileOffset);
            ////Interpreter.RegisterCommandHandler("targettilerelative", TargetTileRelative);
            ////Interpreter.RegisterCommandHandler("settimer", SetTimer);
            ////Interpreter.RegisterCommandHandler("removetimer", RemoveTimer);
            ////Interpreter.RegisterCommandHandler("createtimer", CreateTimer);




            //Interpreter.RegisterCommandHandler("info", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("playmacro", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("playsound", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("snapshot", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("hotkeys", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("where", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("mapuo", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("clickscreen", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("helpbutton", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("guildbutton", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("questsbutton", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("logoutbutton", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("virtue", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("partymsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("guildmsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("allymsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("whispermsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("yellmsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("chatmsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("emotemsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("promptmsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("timermsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("waitforprompt", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("cancelprompt", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("addfriend", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("removefriend", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("contextmenu", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("waitforcontext", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("ignoreobject", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("clearignorelist", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("setskill", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("waitforproperties", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autocolorpick", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("waitforcontents", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("miniheal", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("bigheal", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("chivalryheal", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("moveitemoffset", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("movetype", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("movetypeoffset", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("togglemounted", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("equipwand", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("buy", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("sell", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("clearbuy", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("clearsell", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("organizer", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("counter", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("replygump", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("closegump", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("cleartargetqueue", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargetlast", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargetself", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargetobject", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargettype", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargettile", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargettileoffset", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargettilerelative", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargetghost", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargetground", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("cancelautotarget", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("getenemy", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("getfriend", UnimplementedCommand);
        }

        private static string SetAbility_LastAbility = "";
        private static bool SetAbility(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Pre-parsing and checking args is mandatory to achieve UO Steam behavior         |
            // +====================================================================================+
            var ability = argList.NextAs<string>().ToLower();
            var toggle = argList.NextAs<string>().ToLower();

            if (toggle == String.Empty || toggle == "on")
            {
                string abilityName;
                DebugMsg($"turning {ability} on");
                switch (ability)
                {
                    case "primary":
                        abilityName = Enum.GetName(typeof(Ability), World.Player.Abilities[0]);
                        GameActions.UsePrimaryAbility();  
                        break;
                    case "secondary":
                        abilityName = Enum.GetName(typeof(Ability), World.Player.Abilities[1]);
                        GameActions.UseSecondaryAbility();         
                        break;
                    case "stun":
                        GameActions.RequestStun();
                        abilityName = "Stun";
                        break;
                    case "disarm":
                        GameActions.RequestDisarm();
                        abilityName = "Disarm";
                        break;
                    default:
                        throw new ScriptSyntaxError("invalid ability type");
                }

                // UOSTEAM: If changed, ability needs to be printed in green (when quiet modifier is not used)
                if (!quiet && abilityName != SetAbility_LastAbility)
                {
                    SetAbility_LastAbility = abilityName;
                    GameActions.Print($"Ability: {SetAbility_LastAbility}", hue: 0x44 /*green*/, type: MessageType.System);
                }
            }
            else
            {
                GameActions.ClearAbility();
                DebugMsg($"turning {ability} off");
            }
            return true;
        }

        private static bool Attack(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - No feedback at all if attack does not work                                      |
            // +====================================================================================+
            var serial = argList.NextAs<uint>();
            GameActions.Print($"Attack: attacking {serial}", hue: 0x104, type: MessageType.System);
            GameActions.Attack(serial);
            return true;
        }

        private static bool ClearHands(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Will ignore command (returning success) if holding/dragging an item             |
            // +====================================================================================+
            // If we are already moving item from hand to backpack, keep moving it
            if (OperationMoveItem.CurrentState != OperationMoveItem.State.NotMoving)
            {
                OperationMoveItem.MoveItem();
                return false; // Come back to command even after item finished moving as we may have another hand to handle
            }

            // Read arg and retrieve needed info on hands
            var backpack = World.Player.FindItemByLayer(Layer.Backpack);
            var hand = argList.NextAs<string>().ToLower();
            if(hand != "left" && hand != "right" && hand != "both")
                throw new ScriptCommandError("item layer not found");

            // Clear hands
            if (hand == "left" || hand == "both")
            {
                var item = World.Player.FindItemByLayer(Layer.HeldInHand2);
                if (item != null)
                {
                    Aliases.Write<uint>($"lastleftequipped", item.Serial);
                    OperationMoveItem.MoveItem(item.Serial, backpack.Serial);
                    return false; // Enter move item loop
                }
            }
            if (hand == "right" || hand == "both")
            {
                var item = World.Player.FindItemByLayer(Layer.HeldInHand1);
                if (item != null)
                {
                    Aliases.Write<uint>($"lastrightequipped", item.Serial);
                    OperationMoveItem.MoveItem(item.Serial, backpack.Serial);
                    return false; // Enter move item loop
                }
            }

            // If we reached here we have no more items in the processed hands :)
            return true;
        }

        private static bool ClickObject(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Click msg appears on cursor position if object out of range (we cant do it)     |
            // +====================================================================================+
            var serial = argList.NextAs<uint>();
            if (World.Get(serial) == null)
                throw new ScriptSyntaxError("item or mobile not found");
            else
            {
                GameActions.Print($"ClickObject: single click at {serial}", hue: 0x104, type: MessageType.System);
                GameActions.SingleClick(serial);
            }
            return true;
        }

        private static bool BandageSelf(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Use once requires item to be in the backpack (floor or equipped is ignored)     |
            // +====================================================================================+
            Item bandage = World.Player.FindBandage();
            if (bandage != null)
            {
                GameActions.Print($"BandageSelf: using bandages", hue: 0x104, type: MessageType.System);
                ClassicUO.Network.NetClient.Socket.Send(new PTargetSelectedObject(bandage.Serial, World.Player.Serial));
            }
            else
                throw new ScriptSyntaxError("bandages not found");
            return true;
        }

        private static bool UseType(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Serial has to be an item, Mobiles are ignored                                   |
            // |  - Use once requires item to be in the backpack (floor or equipped is ignored)     |
            // +====================================================================================+
            Item item = CmdFindItemByGraphic(
                argList.NextAs<ushort>(),
                argList.NextAs<ushort>(),
                argList.NextAs<uint>(),
                0,
                argList.NextAs<int>()
                );
            if (item != null)
            {
                GameActions.Print($"UseType: double clicking {item.Serial}", hue: 0x104, type: MessageType.System);
                if (force) // force bypass the item queue
                    GameActions.DoubleClick(item.Serial);
                else
                    GameActions.DoubleClickQueued(item.Serial);
            }
            else
                throw new ScriptSyntaxError("item type not found");
            return true;
        }

        private static bool UseObject(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Item can be anywhere (equipped, floor or accessible containers)                 |
            // |  - Use object works even on mobs and matches to a double click                     |
            // +====================================================================================+
            var serial = argList.NextAs<uint>();
            var entity = CmdFindEntityBySerial(serial);
            if (entity != null)
            {
                GameActions.Print($"UseObject: double clicking {entity.Serial}", hue: 0x104, type: MessageType.System);
                if (force) // force bypass the item queue
                    GameActions.DoubleClick(entity.Serial);
                else
                    GameActions.DoubleClickQueued(entity.Serial);
            }
            else
                throw new ScriptSyntaxError("item type not found");
            return true;
        }

        private static bool UseOnce(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Serial has to be an item, Mobiles are ignored                                   |
            // |  - Use once requires item to be in the backpack (floor or equipped is ignored)     |
            // +====================================================================================+
            Item item = CmdFindItemByGraphic(
                argList.NextAs<ushort>(),
                argList.NextAs<ushort>(),
                World.Player.FindItemByLayer(Layer.Backpack)
            );

            if (item != null)
            {
                GameActions.Print($"UseOnce: double clicking {item.Serial}", hue: 0x104, type: MessageType.System);
                if (force) // force bypass the item queue
                    GameActions.DoubleClick(item.Serial);
                else
                    GameActions.DoubleClickQueued(item.Serial);
            }
            else
                throw new ScriptSyntaxError("item type not found");
            return true;
        }

        private static bool MoveItem(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - If already holding an item, it will move item in hand to destination            |
            // |  - Ground is ignored as a destination                                              |
            // +====================================================================================+
            // If we are already moving item from/to hand, keep moving it
            if (OperationMoveItem.CurrentState != OperationMoveItem.State.NotMoving)
                return (OperationMoveItem.MoveItem() == OperationMoveItem.State.NotMoving); // If item finished moving return true

            // Read arg
            var serial = argList.NextAs<uint>();    
            var destination = argList.NextAs<uint>();
            var x = argList.NextAs<int>();
            var y = argList.NextAs<int>();
            var z = argList.NextAs<int>();
            var amount = argList.NextAs<int>();

            // Check destination
            if (destination == 0)
            {
                throw new ScriptCommandError("destination not found");
            }
            else if (destination == uint.MaxValue)
            {
                DebugMsg("Ground is not accepted as a destination");
                return true;
            }

            // Retrieve item
            var entity = CmdFindEntityBySerial(serial);
            if (entity == null)
            {
                throw new ScriptCommandError("item not found");
            }

            // ATTENTION: the logic of moving an item is used by several commands, so it was implemented as an operation
            OperationMoveItem.MoveItem(serial, destination, x, y, z, amount);
            return OperationMoveItem.CurrentState == OperationMoveItem.State.NotMoving;
        }

        private static bool MoveItemOffset(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Almost same behavior as MoveItem, but "ground" is accepted                      |
            // |  - If already holding an item, it will move item in hand to destination            |
            // +====================================================================================+
            // If we are already moving item from/to hand, keep moving it
            if (OperationMoveItem.CurrentState != OperationMoveItem.State.NotMoving)
                return (OperationMoveItem.MoveItem() == OperationMoveItem.State.NotMoving); // If item finished moving return true

            // Read arg
            var serial = argList.NextAs<uint>();
            var destination = argList.NextAs<uint>();
            var x = argList.NextAs<int>();
            var y = argList.NextAs<int>();
            var z = argList.NextAs<int>();
            var amount = argList.NextAs<int>();

            // Check destination
            if (destination == 0)
            {
                throw new ScriptCommandError("destination not found");
            }
            else if (destination == uint.MaxValue)
            {
                // Ground offset is based on player and floor height
                x += World.Player.X;
                y += World.Player.Y;
                z += World.Map.GetTileZ(x, y);
            }

            // Retrieve item
            var entity = CmdFindEntityBySerial(serial);
            if (entity == null)
            {
                throw new ScriptCommandError("item not found");
            }

            // Start moving it
            OperationMoveItem.MoveItem(serial, destination, x, y, z, amount);
            return OperationMoveItem.CurrentState == OperationMoveItem.State.NotMoving;
        }

        private static bool MoveType(ArgumentList argList, bool quiet, bool force)
        {
            // += UO Steam =========================================================================+
            // |  - Same behavior as MoveItem                                                       |
            // +====================================================================================+
            // If we are already moving item from/to hand, keep moving it
            if (OperationMoveItem.CurrentState != OperationMoveItem.State.NotMoving)
                return (OperationMoveItem.MoveItem() == OperationMoveItem.State.NotMoving); // If item finished moving return true

            // Read arg
            var graphic = argList.NextAs<ushort>();
            var source = argList.NextAs<uint>();
            var destination = argList.NextAs<uint>();
            var x = argList.NextAs<int>();
            var y = argList.NextAs<int>();
            var z = argList.NextAs<int>();
            var color = argList.NextAs<ushort>();
            var amount = argList.NextAs<int>();
            var range = argList.NextAs<int>();

            // Check destination
            if (destination == 0)
            {
                throw new ScriptCommandError("destination not found");
            }
            else if (destination == uint.MaxValue)
            {
                DebugMsg("Ground is not accepted as a destination");
                return true;
            }

            // Retrieve item
            Item item = CmdFindItemByGraphic(graphic, color, source, amount, range);
            if (item == null)
            {
                throw new ScriptCommandError("item not found");
            }

            // Start moving it
            OperationMoveItem.MoveItem(item.Serial, destination, x, y, z, amount);
            return OperationMoveItem.CurrentState == OperationMoveItem.State.NotMoving;
        }

        private static bool MoveTypeOffset(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Almost same behavior as MoveItem, but "ground" is accepted                      |
            // |  - If already holding an item, it will move item in hand to destination            |
            // +====================================================================================+
            // If we are already moving item from/to hand, keep moving it
            if (OperationMoveItem.CurrentState != OperationMoveItem.State.NotMoving)
                return (OperationMoveItem.MoveItem() == OperationMoveItem.State.NotMoving); // If item finished moving return true

            // Read arg
            var graphic = argList.NextAs<ushort>();
            var source = argList.NextAs<uint>();
            var destination = argList.NextAs<uint>();
            var x = argList.NextAs<int>();
            var y = argList.NextAs<int>();
            var z = argList.NextAs<int>();
            var color = argList.NextAs<ushort>();
            var amount = argList.NextAs<int>();
            var range = argList.NextAs<int>();

            // Check destination
            if (destination == 0)
            {
                throw new ScriptCommandError("destination not found");
            }
            else if (destination == uint.MaxValue)
            {
                // Ground offset is based on player and floor height
                x += World.Player.X;
                y += World.Player.Y;
                z += World.Map.GetTileZ(x, y);
            }

            // Retrieve item
            Item item = CmdFindItemByGraphic(graphic, color, source, amount, range);
            if (item == null)
            {
                throw new ScriptCommandError("item not found");
            }

            // Start moving it
            OperationMoveItem.MoveItem(item.Serial, destination, x, y, z, amount);
            return OperationMoveItem.CurrentState == OperationMoveItem.State.NotMoving;
        }

        private static int MovementLogic_MoveIndex = -1;
        public static Command.Handler MovementLogic(bool forceRun = false)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Full array of provided directions is processed (nothing runs in parallel)       |
            // |  - No "help" turn at start of movement, meaning a walk can be just a turn          |
            // |  - Turn moves character and acts exatcly as 'walk'                                 |
            // +====================================================================================+

            // Using currying technique so method can be reused for turn, walk and run
            return (argList, quiet, force) => {

                // handle array to support multiple directions -> walk "North, East, East, West, South, Southeast"
                var dirArray = argList.NextAsArray<string>();

                // Start by reseting index
                if (MovementLogic_MoveIndex < 0)
                    MovementLogic_MoveIndex = 0;

                // Move player
                var direction = (Direction)Enum.Parse(typeof(Direction), dirArray[MovementLogic_MoveIndex++], true);
                World.Player.Walk(direction, forceRun || ProfileManager.CurrentProfile.AlwaysRun);

                // Stop at end of array
                if (MovementLogic_MoveIndex >= dirArray.Length)
                {
                    MovementLogic_MoveIndex = -1;
                    return true;
                }
                return false;
            };
        }

        private static bool UseSkill(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Tells if skill was not found                                                    |
            // +====================================================================================+
            var skill = argList.NextAs<string>().ToLower();
            if (skill == "last")
                GameActions.UseLastSkill();
            else if (!GameActions.UseSkill(skill))
                throw new ScriptCommandError($"invalid skill name '{skill}'");
            return true;
        }

        private static bool Feed(ArgumentList argList, bool quiet, bool force)
        {
            try
            {
                var serial = argList.NextAs<uint>();
                List<ushort> foodList = new List<ushort>();
                //ushort graphic;
                //if (!args.TryAsGraphic(out graphic))
                //{
                //    var source = args.NextAsSource(ParameterList.ArgumentType.Mandatory);
                //    destination = World.Player.FindItemByLayer((Layer)source).Serial;
                //}
                //else foodList.Add(graphic)
                //var color = args.NextAs<ushort>();
                //var amount = args.NextAs<int>();

                //Item item = CmdFindObjectBySerial(serial, color);//, source, range);
                //return item != null && item.Amount > amount;
                return true;
            }
            catch (ScriptRunTimeError ex)
            {
                throw new ScriptSyntaxError("Usage: feed (serial) ('food name'/'food group'/'any'/graphic) [color] [amount]", ex);
            }
        }

        private static bool Rename(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Simple and never fails                                                          |
            // +====================================================================================+
            GameActions.Rename(
                argList.NextAs<uint>(),
                argList.NextAs<string>()); // ATTENTION: maybe the only case that a string should not be lowered
            return true;
        }

        private static bool ShowNames(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Simple and never fails                                                          |
            // +====================================================================================+
            var names = argList.NextAs<string>().ToLower();
            if (names == string.Empty)
                names = "all";

            var target = (GameActions.AllNamesTargets)Enum.Parse(typeof(GameActions.AllNamesTargets), names, true);
            GameActions.AllNames(target);
            return true;
        }

        public static bool ToggleHands(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Check if item exists in provided hand                                           |
            // |  - If equipped force unequip with clear to save cache                              |
            // |  - Inever fails                                                                    |
            // +====================================================================================+
            // If we are already moving item from/to hand, keep moving it
            if (OperationMoveItem.CurrentState != OperationMoveItem.State.NotMoving)
                return (OperationMoveItem.MoveItem() == OperationMoveItem.State.NotMoving); // If item finished moving return true

            // Read arg and retrieve needed info on hands
            var backpack = World.Player.FindItemByLayer(Layer.Backpack);
            var hand = argList.NextAs<string>().ToLower();
            Layer layer;
            switch (hand)
            {
                case "left":
                    layer = Layer.HeldInHand2;
                    break;
                case "right":
                    layer = Layer.HeldInHand1;
                    break;
                default:
                    throw new ScriptCommandError("item layer not found");
            }

            // If item is present on hand, start unequip and save cache
            var item = World.Player.FindItemByLayer(layer);
            if (item != null) 
            {
                Aliases.Write<uint>($"last{hand}equipped", item.Serial);
                OperationMoveItem.MoveItem(item.Serial, backpack.Serial);
            }
            else // Otherwise try equipping cached
            {
                uint cachedSerial = 0;
                if(Aliases.Read<uint>($"last{hand}equipped", ref cachedSerial))
                {
                    //OperationMoveItem.MoveItem(item.Serial, backpack.Serial);
                    if (GameActions.PickUp(cachedSerial, 0, 0, 1))
                        GameActions.Equip(layer);
                }
                else
                    throw new ScriptCommandError("item not found");                 
            }
            return true;
        }

        //private static uint EquipItem_item = 0;
        //private static Layer EquipItem_layer = Layer.Invalid;
        public static bool EquipItem(ArgumentList argList, bool quiet, bool force)
        {
            if (ItemHold.Enabled)
            {
                GameActions.Print($"You are already holding an item", type: MessageType.Command);
                GameActions.DropItem(ItemHold.Serial, ItemHold.X, ItemHold.Y, ItemHold.Z, ItemHold.Container);
                return true;
            }

            var item = World.Get(argList.NextAs<uint>());
            if (item == null)
                throw new ScriptRunTimeError(null, "item not found");

            var layer = (Layer)argList.NextAs<int>();
            if (layer == Layer.Invalid)
                layer = (Layer)ItemHold.ItemData.Layer;

            if (GameActions.PickUp(item.Serial, 0, 0, 1))
                GameActions.Equip(layer);
            return true;

            //// READY - Delete after review
            //// ----------------------------------  UO Steam  --------------------------------------
            ////   Never fails.
            ////   It returns Usage in white if arguments are invalid
            ////   If item already in hand it auto-drops saying: "You are already holding an item"
            ////   BLOCK UNTIL ACTION FINISHES
            //// ------------------------------------------------------------------------------------
            //Interpreter.Timeout(5000, () => {
            //    EquipItem_item = 0;
            //    EquipItem_layer = Layer.Invalid;
            //    GameActions.Print($"EquipItem: TIMEOUT", hue: 0x104, type: MessageType.System);
            //    return true;
            //});
            //if (EquipItem_item == 0) // Pick up the item (locking it on cursor)
            //{
            //    if (ItemHold.Enabled)
            //    {
            //        GameActions.Print($"You are already holding an item", type: MessageType.Command);
            //        GameActions.DropItem(ItemHold.Serial, ItemHold.X, ItemHold.Y, ItemHold.Z, ItemHold.Container);
            //        Interpreter.ClearTimeout();
            //        return true;
            //    }

            //    var item = World.Get(argList.NextAs<uint>());
            //    if (item == null)
            //        throw new ScriptRunTimeError(null, "item not found");

            //    EquipItem_layer = (Layer)argList.NextAs<int>();
            //    if (EquipItem_layer == Layer.Invalid)
            //        EquipItem_layer = (Layer)ItemHold.ItemData.Layer;

            //    EquipItem_item = item.Serial;
            //    GameActions.PickUp(item, 0, 0, 1);
            //    GameActions.Print($"EquipItem: Picking up {EquipItem_item}", hue: 0x104, type: MessageType.System);
            //}
            //else if(EquipItem_layer != Layer.Invalid && ItemHold.Enabled) // Equip
            //{
            //    // Equip item with given layer
            //    EquipItem_item = ItemHold.Serial;
            //    GameActions.Equip(EquipItem_layer);
            //    GameActions.Print($"EquipItem: Equipping up {EquipItem_item}", hue: 0x104, type: MessageType.System);
            //}
            //else if(World.Player.FindItemByLayer(EquipItem_layer)?.Serial == EquipItem_item)
            //{
            //    // Is item finally equipped?
            //    GameActions.Print($"EquipItem: Equipped {EquipItem_item}", hue: 0x104, type: MessageType.System);
            //    EquipItem_item = 0;
            //    EquipItem_layer = Layer.Invalid;
            //    Interpreter.ClearTimeout();
            //    return true;
            //}

            //return false;
        }

        public static bool ToggleMounted(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Prompt for Mount alias if none is found (but cancel command)                    |
            // +====================================================================================+

            // If player has a mount, save mount alias before dismounting
            uint serial = 0;
            Item it = World.Player.FindItemByLayer(Layer.Mount);
            if (it != null)
            {
                // Aliases.Write<uint>("mount", it.Serial); //ATTENTION: Item in Layer.Mount is not the mount that will be in the world after dismounting
                serial = World.Player.Serial;
            }
            else if (!Aliases.Read<uint>("mount", ref serial) || serial == 0) // Otherwise try to find mount alias
            {
                // If nothing is found, prompt alias as well
                if(!Invoke(PromptAlias, false, false, ("alias", "mount")))
                {
                    return false; // As we return false, we force the loop of this command being called again and recalling PromptAlias.
                }
            }

            // Finally, double click mount to mount it, or player to dismount it
            if(serial != 0)
                GameActions.DoubleClick(serial);
            return true;
        }

        public static bool EquipWand(ArgumentList argList, bool quiet, bool force)
        {
            // TODO This should be a system list available via script...
            ushort[] wandGraphicIds = new ushort[] { 0xDF2 };

            // Find all wands based on gaphic
            List<Item> allWands = FindAllByGraphic(wandGraphicIds);
            foreach(Item wand in allWands)
            {
                GameActions.SingleClick(wand.Serial);
                //wand.TextContainer.
                // TODO: As Jaedan mentioned the ItemProperties may not be available in UOO, waiting to sync with him
                // But after processing the propertyes we select one of the wands and call Equip
                //EquipItem(execution);
            }

            return true;
        }

        public static bool Dress(ArgumentList argList, bool quiet, bool force)
        {
            // Check if profile is supported
            var profile = argList.NextAs<string>().ToLower();
            if(profile == string.Empty)
                profile = "dressconfig-temporary";
            else if(!Profiles.ContainsKey(profile))
            {
                GameActions.Print($"dress: profile '{profile}' doesn't exist", type: MessageType.System);
                return false;
            }

            // Dress items in profile
            foreach(var serial in Profiles[profile])
            {
                var item = World.GetOrCreateItem(serial);
                if (item.Layer != item.StaticLayer && (Layer)item.ItemData.Layer != Layer.Invalid) // not equipped and valid
                {
                    // UOStream - behavior is queing a request to move each item
                    //ArgumentList promptParams = new ArgumentList(new Argument[1] { new VirtualArgument($"0x{serial.ToString("X")}") }, Definitions["equipitem"].ArgTypes);
                    //Command.Queues[Definitions["equipitem"].Attribute].Enqueue((new CommandExecution(Definitions["equipitem"], promptParams, execution.Quiet, execution.Force)));

                    //ArgumentList promptParams = new ArgumentList(new Argument[2] { new VirtualArgument($"0x{serial.ToString("X")}"), new VirtualArgument($"0x{destintion.Serial.ToString("X")}") }, Definitions["equipitem"].ArgTypes);
                    //Command.Queues[Definitions["moveitem"].Attribute].Enqueue((new CommandExecution(Definitions["EquipItem"], promptParams, execution.Quiet, execution.Force)));
                }
            }
            return true;
        }

        public static bool Undress(ArgumentList argList, bool quiet, bool force)
        {
            var profile = argList.NextAs<string>();
            if (profile == string.Empty)
            {
                // UO Steam: Should undress all when no profile is provided
                // So let's create a temporaty profile with current equiped items
                profile = "temporary-generic";
                Profiles[profile].Clear();
                for (LinkedObject i = World.Player.Items; i != null; i = i.Next)
                {
                    Item it = (Item)i;
                    // add it to the list if we found it
                    if (it.AllowedToDraw && (it.ItemData.IsWearable || it.ItemData.IsWeapon) && it.Layer != Layer.Invalid && it.Layer != Layer.Backpack && it.Layer != Layer.Bank && it.Layer != Layer.Beard && it.Layer != Layer.Face &&
                        it.Layer != Layer.Hair && it.Layer != Layer.Mount && it.Layer != Layer.ShopBuy && it.Layer != Layer.ShopBuyRestock && it.Layer != Layer.ShopSell)
                    {
                        Profiles[profile].Add(it.Serial);
                    }
                }  
            }
            else if(!Profiles.ContainsKey(profile))
            {
                // Check if provided profile is valid
                GameActions.Print($"dress: profile '{profile}' doesn't exist", type: MessageType.System);
                return false;
            }

            // Undress items in profile
            var backpack = World.Player.FindItemByLayer(Layer.Backpack);
            foreach (var serial in Profiles[profile])
            {
                // UOStream - behavior is queing a request to move each item
                //ArgumentList promptParams = new ArgumentList(new Argument[2] { new VirtualArgument($"0x{serial.ToString("X")}"), new VirtualArgument("backpack") }, Definitions["moveitem"].ArgTypes);
                //Command.Queues[Definitions["moveitem"].Attribute].Enqueue((new CommandExecution(Definitions["moveitem"], promptParams, execution.Quiet, execution.Force)));
            }
            return true;
        }

        public static bool Dressconfig(ArgumentList argList, bool quiet, bool force)
        {
            // Set current equipped items in temporary dressing profile
            var profile = "dressconfig-temporary";
            Profiles[profile].Clear();
            for (LinkedObject i = World.Player.Items; i != null; i = i.Next)
            {
                Item it = (Item)i;
                // add it to the list if we found it
                if (it.AllowedToDraw && (it.ItemData.IsWearable || it.ItemData.IsWeapon) && it.Layer != Layer.Invalid && it.Layer != Layer.Backpack && it.Layer != Layer.Bank && it.Layer != Layer.Beard && it.Layer != Layer.Face &&
                    it.Layer != Layer.Hair && it.Layer != Layer.Mount && it.Layer != Layer.ShopBuy && it.Layer != Layer.ShopBuyRestock && it.Layer != Layer.ShopSell)
                {
                    Profiles[profile].Add(it.Serial);
                }
            }
            return true;
        }

        public static bool FindObject(ArgumentList argList, bool quiet, bool force)
        {
            Entity entity = CmdFindEntityBySerial(
                argList.NextAs<uint>(),
                argList.NextAs<ushort>(),
                argList.NextAs<uint>(),
                argList.NextAs<int>(),
                argList.NextAs<int>()
                );
            if(entity != null)
            {
                Aliases.Write<uint>("found", entity.Serial);
                return true;
            }
            else return false;
        }

        private static bool PopList(ArgumentList argList, bool quiet, bool force)
        {
            var listName = argList.NextAs<string>();
            var value = argList.NextAs<string>();

            if (value == "front")
            {
                if (force)
                    while (Interpreter.PopList(listName, true)) { }
                else
                    Interpreter.PopList(listName, true);
            }
            else if (value == "back")
            {
                if (force)
                    while (Interpreter.PopList(listName, false)) { }
                else
                    Interpreter.PopList(listName, false);
            }
            else
            {
                if (force)
                    while (Interpreter.PopList(listName, argList[1])) { }
                else
                    Interpreter.PopList(listName, argList[1]);
            }

            return true;
        }

        private static bool RemoveList(ArgumentList argList, bool quiet, bool force)
        {
            var listName = argList.NextAs<string>();
            Interpreter.DestroyList(listName);
            return true;
        }

        private static bool CreateList(ArgumentList argList, bool quiet, bool force)
        {
            var listName = argList.NextAs<string>();
            Interpreter.CreateList(listName);
            return true;
        }

        private static bool ClearList(ArgumentList argList, bool quiet, bool force)
        {
            var listName = argList.NextAs<string>();
            Interpreter.ClearList(listName);
            return true;
        }

        private static bool PushList(ArgumentList argList, bool quiet, bool force)
        {
            var name = argList.NextAs<string>();
            var values = argList.NextAsArray<string>();
            var pos = argList.NextAs<string>();
            bool front = (pos == "force");
            foreach (var val in values)
            {
                //Interpreter.PushList(name, new Argument(), front, force);
            }
            return true;
        }

        public static bool Msg(ArgumentList argList, bool quiet, bool force)
        {
            GameActions.Say(
                argList.NextAs<string>(),
                hue: argList.NextAs<ushort>()
                );
            return true;
        }

        private static bool SetAlias(ArgumentList argList, bool quiet, bool force)
        {
            Aliases.Write<uint>(
                argList.NextAs<string>(),
                argList.NextAs<uint>()
                );
            return true;
        }

        private static bool UnsetAlias(ArgumentList argList, bool quiet, bool force)
        {
            Aliases.Remove(typeof(uint), argList.NextAs<string>());
            return true;
        }

        private static bool FindType(ArgumentList argList, bool quiet, bool force)
        {
            Item item = CmdFindItemByGraphic(
                argList.NextAs<ushort>(),
                argList.NextAs<ushort>(),
                argList.NextAs<uint>(),
                argList.NextAs<int>(),
                argList.NextAs<int>()
                );

            if (item != null)
            {
                Aliases.Write<uint>("found", item.Serial);
                return true;
            }
            else return false;
        }

        private static bool PromptAlias_prompted = false;
        private static bool PromptAlias(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - If target already exists, it will hijack it                                     |
            // +====================================================================================+
            Interpreter.Timeout(60000, () => {
                throw new ScriptCommandError("Timeout");
            });

            if(!PromptAlias_prompted)
            {
                TargetManager.SetTargeting(CursorTarget.Object, 0, TargetType.Neutral);
                PromptAlias_prompted = true;
            }
            else if (!TargetManager.IsTargeting)
            {
                var alias = argList.NextAs<string>().ToLower();
                Aliases.Write<uint>(
                    alias,
                    TargetManager.LastTargetInfo.Serial
                );
                Interpreter.ClearTimeout();
                PromptAlias_prompted = false;
                return true;
            }

            return false;
        }

        private static bool FindAlias(ArgumentList argList, bool quiet, bool force)
        {
            var alias = argList.NextAs<string>().ToLower();
            uint value = 0;
            return Aliases.Read<uint>(alias, ref value);
        }

        private static bool WaitForGump(ArgumentList argList, bool quiet, bool force)
        {
            var gumpid = argList.NextAs<uint>(); // GumpID is also a serial
            var timeout = argList.NextAs<int>(); // Timeout in ms

            Interpreter.Pause(60000);
            //Task.Run(() =>
            //{
            //    // Take a snapshot of all existing gumps
            //    var gumpsSnapshot = UIManager.Gumps.ToArray();

            //    DateTime startTime = DateTime.UtcNow;
            //    while (DateTime.UtcNow - startTime < TimeSpan.FromMilliseconds(timeout))
            //    {
            //        if (gumpid == 0 /*Zero value is Any*/) // any new gump was added
            //        {
            //            // For each existing gump
            //            for (LinkedListNode<Gump> first = UIManager.Gumps.First; first != null; first = first.Next)
            //            {
            //                // If its new (does not exist in snapshot)
            //                if(!gumpsSnapshot.Contains(first.Value))
            //                {
            //                    // Stop waiting if its in an interactable state
            //                    if (first.Value.IsModal || first.Value.IsVisible || first.Value.IsEnabled)
            //                    {
            //                        Interpreter.Unpause();
            //                        return;
            //                    }
            //                }
            //            }
            //        }
            //        else
            //        {
            //            var specificGump = UIManager.GetGump(gumpid);
            //            // Requested gump exists and is visible
            //            if (specificGump != null && (specificGump.IsModal && !specificGump.IsModal || !specificGump.IsVisible || !specificGump.IsEnabled))
            //            {
            //                Interpreter.Unpause();
            //                return;
            //            }
            //            return;  
            //        }

            //        Thread.Sleep(25); // keep iteration
            //    }
            //    Interpreter.Unpause();
            //});
            return true;
        }

        private static bool ClickScreen(ArgumentList argList, bool quiet, bool force)
        {
            // Read command arguments
            var mouseX = argList.NextAs<int>();
            var mouseY = argList.NextAs<int>();
            Mouse.Position.X = mouseX;
            Mouse.Position.Y = mouseY;
            var clickType = argList.NextAs<string>();
            MouseButtonType buttonType = MouseButtonType.Left;
            if (argList.NextAs<string>() == "right")
                buttonType = MouseButtonType.Right;

            // STOP - can we use DelayedObjectClickManager.Set(ent.Serial, Mouse.Position.X, Mouse.Position.Y, Time.Ticks + Mouse.MOUSE_DELAY_DOUBLE_CLICK); ????????????

            // Get scene and inject mouse down logic
            GameScene gs = Client.Game.GetScene<GameScene>();
            if (clickType == string.Empty || clickType == "single")
            {
                UIManager.OnMouseButtonDown(buttonType);
                gs.OnMouseDown(buttonType);
            }
            else
            {
                UIManager.OnMouseDoubleClick(buttonType);
                gs.OnMouseDoubleClick(buttonType);
            }

            // Because mouse  logic is nested with draw logic, we need to force for the game to darw
            Interpreter.Pause(60000);
            //Task.Run(() =>
            // {
            //     Mouse.Position.X = mouseX;
            //     Mouse.Position.Y = mouseY;
            //     Mouse.ButtonRelease(buttonType);
            //     UIManager.OnMouseButtonUp(buttonType);
            //     gs.OnMouseUp(buttonType);
            //     Mouse.Update(); // reset mouse with real inout
            //     Thread.Sleep(100); // keep iteration (next frame?)
            //     Interpreter.Unpause();
            // });
            return true;
        }

        private static bool WaitForTarget(ArgumentList argList, bool quiet, bool force)
        {
            Interpreter.Timeout(argList.NextAs<int>(), () => { return true; });
            if (TargetManager.IsTargeting)
            {
                Interpreter.ClearTimeout();
                return true;
            }
            else return false;
            //return !TargetManager.IsTargeting;

            //var timeout = argList.NextAs<int>();

            //Interpreter.Pause(timeout);
            //Task.Run(() =>
            //{
            //    // Take a snapshot of all existing gumps
            //    var gumpsSnapshot = UIManager.Gumps.ToArray();

            //    DateTime startTime = DateTime.UtcNow;
            //    while (DateTime.UtcNow - startTime < TimeSpan.FromMilliseconds(timeout))
            //    {
            //        if (TargetManager.IsTargeting)
            //            break;
            //        else
            //            Thread.Sleep(25); // keep iteration
            //    }
            //    Interpreter.Unpause();
            //});

            //return true;
        }

        private static int Pause_Time = 0;
        private static bool Pause(ArgumentList argList, bool quiet, bool force)
        {
            Interpreter.Pause(argList.NextAs<int>());
            return true;
        }

        private static bool ClickTarget(ArgumentList argList, bool quiet, bool force)
        {
            var serial = argList.NextAs<uint>();
            TargetManager.Target(serial);
            return true;
        }

        ////private static bool ClearJournal(string command, ParameterList args)
        ////{
        ////    Journal.Clear();

        ////    return true;
        ////}

        ////private static bool WaitForJournal(string command, ParameterList args)
        ////{
        ////    if (args.Length < 2)
        ////        throw new RunTimeError(null, "Usage: waitforjournal ('text') (timeout) ['author'/'system']");

        ////    if (!Journal.ContainsSafe(args[0].As<string>()))
        ////    {
        ////        Interpreter.Timeout(args[1].AsUInt(), () => { return true; });
        ////        return false;
        ////    }

        ////    return true;
        ////}

        ////public static bool ToggleScavenger(string command, ParameterList args)
        ////{
        ////    ScavengerAgent.Instance.ToggleEnabled();

        ////    return true;
        ////}



        ////private static bool Ping(string command, ParameterList args)
        ////{
        ////    Assistant.Ping.StartPing(5);

        ////    return true;
        ////}

        ////private static bool Resync(string command, ParameterList args)
        ////{
        ////    Client.Instance.SendToServer(new ResyncReq());

        ////    return true;
        ////}

        ////private static bool MessageBox(string command, ParameterList args)
        ////{
        ////    if (args.Length != 2)
        ////        throw new RunTimeError(null, "Usage: messagebox ('title') ('body')");

        ////    System.Windows.Forms.MessageBox.Show(args[0].As<string>(), args[1].As<string>());

        ////    return true;
        ////}



        //private static bool Paperdoll(string command, ParameterList args)
        //{
        //    if (args.Length > 1)
        //        throw new RunTimeError(null, "Usage: paperdoll [serial]");

        //    uint serial = args.Length == 0 ? World.Player.Serial.Value : args[0].AsSerial();
        //    Client.Instance.SendToServer(new DoubleClick(serial));

        //    return true;
        //}

        //public static bool Cast(string command, ParameterList args)
        //{
        //    if (args.Length == 0)
        //        throw new RunTimeError(null, "Usage: cast 'spell' [serial]");

        //    Spell spell;

        //    if (int.TryParse(args[0].As<string>(), out int spellnum))
        //        spell = Spell.Get(spellnum);
        //    else
        //        spell = Spell.GetByName(args[0].As<string>());
        //    if (spell != null)
        //    {
        //        if (args.Length > 1)
        //        {
        //            Serial s = args[1].AsSerial();
        //            if (force)
        //                Targeting.ClearQueue();
        //            if (s > Serial.Zero && s != Serial.MinusOne)
        //            {
        //                Targeting.Target(s);
        //            }
        //            else if (!quiet)
        //                throw new RunTimeError(null, "cast - invalid serial or alias");
        //        }
        //    }
        //    else if (!quiet)
        //        throw new RunTimeError(null, "cast - spell name or number not valid");

        //    return true;
        //}

        //private static bool CancelTarget(string command, ParameterList args)
        //{
        //    if (args.Length != 0)
        //        throw new RunTimeError(null, "Usage: canceltarget");

        //    if (Targeting.HasTarget)
        //        Targeting.CancelOneTimeTarget();

        //    return true;
        //}

        //private static bool Target(string command, ParameterList args)
        //{
        //    if (args.Length != 1)
        //        throw new RunTimeError(null, "Usage: target (serial)");

        //    if (!Targeting.HasTarget)
        //        ScriptManager.Error(quiet, command, "No target cursor available. Consider using waitfortarget.");
        //    else
        //        Targeting.Target(args[0].AsSerial());

        //    return true;
        //}

        //private static bool TargetType(string command, ParameterList args)
        //{
        //    if (args.Length < 1 || args.Length > 3)
        //        throw new RunTimeError(null, "Usage: targettype (graphic) [color] [range]");

        //    if (!Targeting.HasTarget)
        //    {
        //        ScriptManager.Error(quiet, command, "No target cursor available. Consider using waitfortarget.");
        //        return true;
        //    }

        //    var graphic = args[0].AsInt();

        //    uint serial = Serial.MinusOne;

        //    switch (args.Length)
        //    {
        //        case 1:
        //            // Only graphic
        //            serial = World.FindItemByType(graphic).Serial;
        //            break;
        //        case 2:
        //            {
        //                // graphic and color
        //                var color = args[1].AsUShort();
        //                foreach (var item in World.Items.Values)
        //                {
        //                    if (item.ItemID.Value == graphic && item.Hue == color)
        //                    {
        //                        serial = item.Serial;
        //                        break;
        //                    }
        //                }
        //                break;
        //            }
        //        case 3:
        //            {
        //                // graphic, color, range
        //                var color = args[1].AsUShort();
        //                var range = args[2].AsInt();
        //                foreach (var item in World.Items.Values)
        //                {
        //                    if (item.ItemID.Value == graphic && item.Hue == color && Utility.Distance(item.Position, World.Player.Position) < range)
        //                    {
        //                        serial = item.Serial;
        //                        break;
        //                    }
        //                }
        //                break;
        //            }

        //    }

        //    if (serial == Serial.MinusOne)
        //        throw new RunTimeError(null, "Unable to find suitable target");

        //    Targeting.Target(serial);
        //    return true;
        //}

        //private static bool TargetGround(string command, ParameterList args)
        //{
        //    if (args.Length < 1 || args.Length > 3)
        //        throw new RunTimeError(null, "Usage: targetground (graphic) [color] [range]");

        //    throw new RunTimeError(null, $"Unimplemented command {command}");
        //}

        //private static bool TargetTile(string command, ParameterList args)
        //{
        //    if (!(args.Length == 1 || args.Length == 3))
        //        throw new RunTimeError(null, "Usage: targettile ('last'/'current'/(x y z))");

        //    if (!Targeting.HasTarget)
        //    {
        //        ScriptManager.Error(quiet, command, "No target cursor available. Consider using waitfortarget.");
        //        return true;
        //    }

        //    Point3D position = Point3D.MinusOne;

        //    switch (args.Length)
        //    {
        //        case 1:
        //            {
        //                var alias = args[0].As<string>();
        //                if (alias == "last")
        //                {
        //                    if (Targeting.LastTargetInfo.Type != 1)
        //                        throw new RunTimeError(null, "Last target was not a ground target");

        //                    position = new Point3D(Targeting.LastTargetInfo.X, Targeting.LastTargetInfo.Y, Targeting.LastTargetInfo.Z);
        //                }
        //                else if (alias == "current")
        //                {
        //                    position = World.Player.Position;
        //                }
        //                break;
        //            }
        //        case 3:
        //            position = new Point3D(args[0].AsInt(), args[1].AsInt(), args[2].AsInt());
        //            break;
        //    }

        //    if (position == Point3D.MinusOne)
        //        throw new RunTimeError(null, "Usage: targettile ('last'/'current'/(x y z))");

        //    Targeting.Target(position);
        //    return true;
        //}

        //private static bool TargetTileOffset(string command, ParameterList args)
        //{
        //    if (args.Length != 3)
        //        throw new RunTimeError(null, "Usage: targettileoffset (x y z)");

        //    if (!Targeting.HasTarget)
        //    {
        //        ScriptManager.Error(quiet, command, "No target cursor available. Consider using waitfortarget.");
        //        return true;
        //    }

        //    var position = World.Player.Position;

        //    position.X += args[0].AsInt();
        //    position.Y += args[1].AsInt();
        //    position.Z += args[2].AsInt();

        //    Targeting.Target(position);
        //    return true;
        //}

        //private static bool TargetTileRelative(string command, ParameterList args)
        //{
        //    if (args.Length != 2)
        //        throw new RunTimeError(null, "Usage: targettilerelative (serial) (range). Range may be negative.");

        //    if (!Targeting.HasTarget)
        //    {
        //        ScriptManager.Error(quiet, command, "No target cursor available. Consider using waitfortarget.");
        //        return true;
        //    }

        //    var serial = args[0].AsSerial();
        //    var range = args[1].AsInt();

        //    var mobile = World.FindMobile(serial);

        //    if (mobile == null)
        //    {
        //        /* TODO: Search items if mobile not found. Although this isn't very useful. */
        //        ScriptManager.Error(quiet, command, "item or mobile not found.");
        //        return true;
        //    }

        //    var position = mobile.Position;

        //    switch (mobile.Direction)
        //    {
        //        case Direction.North:
        //            position.Y -= range;
        //            break;
        //        case Direction.Right:
        //            position.X += range;
        //            position.Y -= range;
        //            break;
        //        case Direction.East:
        //            position.X += range;
        //            break;
        //        case Direction.Down:
        //            position.X += range;
        //            position.Y += range;
        //            break;
        //        case Direction.South:
        //            position.Y += range;
        //            break;
        //        case Direction.Left:
        //            position.X -= range;
        //            position.Y += range;
        //            break;
        //        case Direction.West:
        //            position.X -= range;
        //            break;
        //        case Direction.Up:
        //            position.X -= range;
        //            position.Y -= range;
        //            break;
        //    }

        //    Targeting.Target(position);

        //    return true;
        //}

        //public static bool HeadMsg(string command, ParameterList args)
        //{
        //    switch (args.Length)
        //    {
        //        case 1:
        //            World.Player.OverheadMessage(Config.GetInt("SysColor"), args[0].As<string>());
        //            break;
        //        case 2:
        //            World.Player.OverheadMessage(args[1].AsInt(), args[0].As<string>());
        //            break;
        //        case 3:
        //            Mobile m = World.FindMobile(args[2].AsSerial());

        //            if (m != null)
        //                m.OverheadMessage(args[1].AsInt(), args[0].As<string>());
        //            break;
        //        default:
        //            throw new RunTimeError(null, "Usage: headmsg (text) [color] [serial]");
        //    }

        //    return true;
        //}

        //public static bool SysMsg(string command, ParameterList args)
        //{
        //    switch (args.Length)
        //    {
        //        case 1:
        //            World.Player.SendMessage(Config.GetInt("SysColor"), args[0].As<string>());
        //            break;
        //        case 2:
        //            World.Player.SendMessage(args[1].AsInt(), args[0].As<string>());
        //            break;
        //        default:
        //            throw new RunTimeError(null, "Usage: sysmsg ('text') [color]");
        //    }

        //    return true;
        //}


        //private static bool SetTimer(string command, ParameterList args)
        //{
        //    if (args.Length != 2)
        //        throw new RunTimeError(null, "Usage: settimer (timer name) (value)");


        //    Interpreter.SetTimer(args[0].As<string>(), args[1].AsInt());
        //    return true;
        //}

        //private static bool RemoveTimer(string command, ParameterList args)
        //{
        //    if (args.Length != 1)
        //        throw new RunTimeError(null, "Usage: removetimer (timer name)");

        //    Interpreter.RemoveTimer(args[0].As<string>());
        //    return true;
        //}

        //private static bool CreateTimer(string command, ParameterList args)
        //{
        //    if (args.Length != 1)
        //        throw new RunTimeError(null, "Usage: createtimer (timer name)");

        //    Interpreter.CreateTimer(args[0].As<string>());
        //    return true;
        //}

        // HELPER FUNCTIONS FOR THE COMMANDS

        private static bool UnsupportedCmd(ArgumentList argList, bool quiet, bool force)
        {
            throw new ScriptCommandError("not supported");
        }

        internal static Entity CmdFindEntityBySerial(uint serial, ushort color = ushort.MaxValue, uint source = 0, int amount = 0, int range = 0)
        {
            // Try fetching and Item from Serial
            Entity entity = null;
            var graphic = World.GetOrCreateItem(serial).Graphic;

            if (source == 0 /*Zero value is Any*/)
            {
                // Any also look at the ground if not found in player belongings
                entity = World.Player.FindItem(graphic, color);
                if (entity != null)
                    entity = World.Player.FindItemByTypeOnGroundWithHueInRange(graphic, color, range);
            }
            else if (source == uint.MaxValue /*Max value is Ground*/)
                entity = World.Player.FindItemByTypeOnGroundWithHueInRange(graphic, color, range);
            else
            {
                //var layer = (Layer)Enum.Parse(typeof(Layer), source, true);
                //entity = World.Player.FindItemByLayer(layer)?.FindItem(graphic, color);
                entity = World.GetOrCreateItem(source).FindItem(graphic, color);
            }

            // If we have an item, check ammount
            if (entity != null && entity is Item item && item.Amount < amount)
                return null;

            // Try fetching a Mobile from Serial
            if (entity == null)
                entity = World.GetOrCreateMobile(serial);
            
            return entity;
        }

        private static Item CmdFindItemByGraphic(ushort graphic, ushort color = ushort.MaxValue, uint source = 0, int amount = 0, int range = 0)
        {
            Item item = null;
            if (source == 0 /*Zero value is Any*/)
            {
                item = World.Player.FindItem(graphic, color);
                if (item == null) // For Any, also look at the ground if not found in player belongings
                    item = World.Player.FindItemByTypeOnGroundWithHueInRange(graphic, color, range);
            }
            else if (source == uint.MaxValue /*Max value is Ground*/)
                item = World.Player.FindItemByTypeOnGroundWithHueInRange(graphic, color, range == 0 ? 12 : range);
            else
            {
                item = World.GetOrCreateItem(source).FindItem(graphic, color);
                //var layer = (Layer)Enum.Parse(typeof(Layer), source, true);
                //item = World.Player.FindItemByLayer(layer)?.FindItem(graphic, color);
            }

            if (item != null && item.Amount < amount)
                return null;
            else return item;
        }

        private static List<Item> FindAllByGraphic(ushort[] graphicIds, ushort color = 0xFFFF, LinkedObject link = null)
        {
            // If no link is provided, use Player items (ecursion starting points)
            if(link == null)
                link = World.Player.Items;

            // Traverse all items in the link
            List<Item> result = new List<Item>();            
            foreach (ushort graphic in graphicIds)
            {
                for (LinkedObject i = link; i != null; i = i.Next)
                {
                    Item it = (Item)i;
                    // add it to the list if we found it
                    if (it.Graphic == graphic && it.Hue < color)
                    {
                        result.Add(it);
                    }
                    // And if a container keep recursion inside
                    if (SerialHelper.IsValid(it.Container) && !it.IsEmpty)
                    {
                        result.AddRange(FindAllByGraphic(graphicIds, color, it.Items));
                    }
                }
            }

            return result;
        }
    }
}
