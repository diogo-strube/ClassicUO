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
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.IO.Audio;
using ClassicUO.IO.Resources;
using ClassicUO.Network;
using ClassicUO.Resources;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Scripting
{
    // Class grouping all command related functionality, including implemented handles
    public static class Commands
    {
        // Helper functions to register command execution
        internal static void AddHandler(string usage, Command.Handler execLogic, int waitTime = 200, CommandGroup group = CommandGroup.None)
        {
            var cmd = new Command(usage, execLogic, waitTime, group);
            Interpreter.RegisterCommandHandler(cmd.Keyword, cmd.Execute);
        }
        internal static void AddHandler(Command cmd)
        {
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

            // Heavly tested Commands
            AddHandler("moveitem (serial) (destination) [(x, y, z)] [amount]", MoveItem, 800, CommandGroup.PickUp);
            AddHandler("moveitemoffset (serial) (destination) [(x, y, z)] [amount]", MoveItemOffset, 800, CommandGroup.PickUp);
            AddHandler("movetype (graphic) (source) (destination) [(x, y, z)] [color] [amount] [range or search level]", MoveType, 800, CommandGroup.PickUp);
            AddHandler("movetypeoffset (graphic) (source) 'ground' [(x, y, z)] [color] [amount] [range or search level]", MoveTypeOffset, 800, CommandGroup.PickUp);
            AddHandler(new MovementCommand("walk (direction)", WalkTurnRun(false)));
            AddHandler(new MovementCommand("turn (direction)", WalkTurnRun(false)));
            AddHandler(new MovementCommand("run (direction)", WalkTurnRun(true)));
            AddHandler("clearhands ('left'/'right'/'both')", ClearHands, 800, CommandGroup.PickUp);
            AddHandler("togglehands ('left'/'right')", ToggleHands, 800, CommandGroup.PickUp);
            AddHandler("equipitem (serial) [layer]", EquipItem, 800, CommandGroup.PickUp);
            AddHandler("togglemounted", ToggleMounted, 800);
            AddHandler("msg ('text') [color]", SayMsg, 800);
            AddHandler("partymsg ('text')", SayPartyMsg, 800);
            AddHandler("guildmsg ('text')", SayMsgType(MessageType.Guild), 800);
            AddHandler("allymsg ('text')", SayMsgType(MessageType.Alliance), 800);
            AddHandler("whispermsg ('text')", SayMsgType(MessageType.Whisper), 800);
            AddHandler("yellmsg ('text')", SayMsgType(MessageType.Yell), 800);
            AddHandler("sysmsg ('text')", PrintMsgType(MessageType.System), 800);
            AddHandler("emotemsg ('text')", SayMsgType(MessageType.Emote), 800);
            AddHandler("headmsg ('text') [color] [serial]", HeadMsg, 800);
            AddHandler("pause (timeout)", Pause);
            AddHandler("promptalias ('alias name')", PromptAlias);

            // Tested Commands
            AddHandler("attack (serial)", Attack);
            AddHandler("setability ('primary'/'secondary'/'stun'/'disarm') ['on'/'off']", SetAbility);
            AddHandler("findtype (graphic) [color] [source] [amount] [range or search level]", FindType);
            AddHandler("bandageself", BandageSelf);
            AddHandler("paperdoll [serial]", Paperdoll);
            AddHandler("clickobject (serial)", ClickObject);
            AddHandler("usetype (graphic) [color] [source] [range or search level]", UseType);
            AddHandler("useobject (serial)", UseObject);
            AddHandler("useonce (graphic) [color]", UseOnce);
            AddHandler("messagebox ('title') ('body')", MessageBox);

            // Accepted Commands (but need more testing to guarantee robustness)
            AddHandler("useskill ('skill name'/'last')", UseSkill);
            AddHandler("feed (serial) ('food name'/'food group'/'any'/graphic) [color] [amount]", Feed);
            AddHandler("rename (serial) ('name')", Rename);
            AddHandler("shownames ['mobiles'/'corpses']", ShowNames);
            AddHandler("info", Info);
            AddHandler("ping", Ping);
            //AddDefinition("equipwand ('spell name'/'any'/'undefined') [minimum charges]", EquipWand, WaitForMs(500), Command.Attributes.ComplexInterAction);
            //AddDefinition("buy ('list name')", Buy, WaitForMs(500), Command.Attributes.ComplexInterAction);
            //AddDefinition("sell ('list name')", Sell, WaitForMs(500), Command.Attributes.ComplexInterAction);
            //AddDefinition("clearbuy", ClearBuy, WaitForMs(500), Command.Attributes.ComplexInterAction);
            //AddDefinition("clearsell", ClearSell, WaitForMs(500), Command.Attributes.ComplexInterAction);
            //AddDefinition("organizer ('profile name') [source] [destination]", Organizer, WaitForMs(500), Command.Attributes.ComplexInterAction);
            //AddDefinition("organizing", Organizing, WaitForMs(500), Command.Attributes.ComplexInterAction);
            AddHandler("dress ['profile name']", Dress);
            AddHandler("undress ['profile name']", Undress);
            AddHandler("dressconfig", Dressconfig);          
            //AddDefinition("togglescavenger", UnsupportedCmd, WaitForMs(25));         
            AddHandler("findobject (serial) [color] [source] [amount] [range]", FindObject);
            AddHandler("poplist ('list name') ('element value'/'front'/'back')", PopList);
            AddHandler("pushlist ('list name') ('element value') ['front'/'back']", PushList);
            AddHandler("createlist ('list name')", CreateList);
            AddHandler("removelist ('list name')", RemoveList);           
            AddHandler("timermsg ('timer name') [color]", TimerMsg, 800);
            AddHandler("setalias ('alias name') [serial]", SetAlias);
            AddHandler("unsetalias ('alias name')", UnsetAlias);    
            AddHandler("findalias ('alias name')", FindAlias);
            AddHandler("waitforgump (gump id/'any') (timeout)", WaitForGump);
            AddHandler("waitfortarget (timeout)", WaitForTarget);         
            AddHandler("target (serial)", ClickTarget);
            AddHandler("playsound (sound id/'file name')", PlaySound);
            AddHandler("playmacro ('macro name')", PlayMacro);
            AddHandler("clearjournal", ClearJournal);
            AddHandler("waitforjournal ('text') (timeout) ['author'/'system']", WaitForJournal);
            AddHandler("clearlist", ClearList);
            AddHandler("canceltarget", CancelTarget);

            // Unsupprted
            AddHandler("autoloot", UnsupportedCmd);
            AddHandler("toggleautoloot", UnsupportedCmd);
            AddHandler("chatmsg ('text')", UnsupportedCmd);
            AddHandler("promptmsg ('text')", UnsupportedCmd);
            AddHandler("waitforprompt (timeout)", UnsupportedCmd);
            AddHandler("cancelprompt", UnsupportedCmd);
            AddHandler("clickscreen (x) (y) ['single'/'double'] ['left'/'right']", UnsupportedCmd);
            AddHandler("resync", UnsupportedCmd);
            AddHandler("clearuseonce", UnsupportedCmd);
            AddHandler("where", Where);

            //Interpreter.RegisterCommandHandler("fly", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("land", UnimplementedCommand);
            ////Interpreter.RegisterCommandHandler("togglescavenger", ToggleScavenger);

            ////Interpreter.RegisterCommandHandler("cast", Cast);

            ////Interpreter.RegisterCommandHandler("target", Target);
            ////Interpreter.RegisterCommandHandler("targettype", TargetType);
            ////Interpreter.RegisterCommandHandler("targetground", TargetGround);
            ////Interpreter.RegisterCommandHandler("targettile", TargetTile);
            ////Interpreter.RegisterCommandHandler("targettileoffset", TargetTileOffset);
            ////Interpreter.RegisterCommandHandler("targettilerelative", TargetTileRelative);
            ////Interpreter.RegisterCommandHandler("settimer", SetTimer);
            ////Interpreter.RegisterCommandHandler("removetimer", RemoveTimer);
            ////Interpreter.RegisterCommandHandler("createtimer", CreateTimer);

            //Interpreter.RegisterCommandHandler("snapshot", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("hotkeys", UnimplementedCommand);

            //Interpreter.RegisterCommandHandler("mapuo", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("clickscreen", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("helpbutton", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("guildbutton", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("questsbutton", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("logoutbutton", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("virtue", UnimplementedCommand);
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
            if (OperationWithItem.CurrentState != OperationWithItem.State.Nothing)
            {
                OperationWithItem.MoveItem();
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
                    OperationWithItem.MoveItem(item.Serial, backpack.Serial);
                    return false; // Enter move item loop
                }
            }
            if (hand == "right" || hand == "both")
            {
                var item = World.Player.FindItemByLayer(Layer.HeldInHand1);
                if (item != null)
                {
                    Aliases.Write<uint>($"lastrightequipped", item.Serial);
                    OperationWithItem.MoveItem(item.Serial, backpack.Serial);
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
            if (OperationWithItem.CurrentState != OperationWithItem.State.Nothing)
                return (OperationWithItem.MoveItem() == OperationWithItem.State.Nothing); // If item finished moving return true

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
            OperationWithItem.MoveItem(serial, destination, x, y, z, amount);
            return OperationWithItem.CurrentState == OperationWithItem.State.Nothing;
        }

        private static bool MoveItemOffset(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Almost same behavior as MoveItem, but "ground" is accepted                      |
            // |  - If already holding an item, it will move item in hand to destination            |
            // +====================================================================================+
            // If we are already moving item from/to hand, keep moving it
            if (OperationWithItem.CurrentState != OperationWithItem.State.Nothing)
                return (OperationWithItem.MoveItem() == OperationWithItem.State.Nothing); // If item finished moving return true

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
            OperationWithItem.MoveItem(serial, destination, x, y, z, amount);
            return OperationWithItem.CurrentState == OperationWithItem.State.Nothing;
        }

        private static bool MoveType(ArgumentList argList, bool quiet, bool force)
        {
            // += UO Steam =========================================================================+
            // |  - Same behavior as MoveItem                                                       |
            // +====================================================================================+
            // If we are already moving item from/to hand, keep moving it
            if (OperationWithItem.CurrentState != OperationWithItem.State.Nothing)
                return (OperationWithItem.MoveItem() == OperationWithItem.State.Nothing); // If item finished moving return true

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
            OperationWithItem.MoveItem(item.Serial, destination, x, y, z, amount);
            return OperationWithItem.CurrentState == OperationWithItem.State.Nothing;
        }

        private static bool MoveTypeOffset(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Almost same behavior as MoveItem, but "ground" is accepted                      |
            // |  - If already holding an item, it will move item in hand to destination            |
            // +====================================================================================+
            // If we are already moving item from/to hand, keep moving it
            if (OperationWithItem.CurrentState != OperationWithItem.State.Nothing)
                return (OperationWithItem.MoveItem() == OperationWithItem.State.Nothing); // If item finished moving return true

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
            OperationWithItem.MoveItem(item.Serial, destination, x, y, z, amount);
            return OperationWithItem.CurrentState == OperationWithItem.State.Nothing;
        }

        public static Command.Handler WalkTurnRun(bool forceRun = false)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Full array of provided directions is processed (nothing runs in parallel)       |
            // |  - No "help" turn at start of movement, meaning a walk can be just a turn          |
            // |  - Turn moves character and acts exatcly as 'walk'                                 |
            // +====================================================================================+
            // ATTENTION: movement seems to be somewhat affected by player latency.. we are ignoring that

            // Using currying technique so method can be reused for turn, walk and run
            return (argList, quiet, force) => {

                // handle array to support multiple directions -> walk "North, East, East, West, South, Southeast"
                var dirArray = argList.NextAsArray<string>();

                // Start by reseting index
                if (MovementCommand.MoveIndex < 0)
                    MovementCommand.MoveIndex = 0;

                // Move player
                var direction = (Direction)Enum.Parse(typeof(Direction), dirArray[MovementCommand.MoveIndex++], true);
                World.Player.Walk(direction, forceRun || ProfileManager.CurrentProfile.AlwaysRun);

                // Stop at end of array
                if (MovementCommand.MoveIndex >= dirArray.Length)
                {
                    MovementCommand.MoveIndex = -1;
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
            // |  - Simple (direct call) and never (ignore) fails                                   |
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
            if (OperationWithItem.CurrentState != OperationWithItem.State.Nothing)
                return (OperationWithItem.MoveItem() == OperationWithItem.State.Nothing); // If item finished moving return true

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
                OperationWithItem.MoveItem(item.Serial, backpack.Serial);
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

        public static bool EquipItem(ArgumentList argList, bool quiet, bool force)
        {
            // += UO Steam =========================================================================+
            // |  - Same behavior as MoveItem but for Layers only                                   |
            // +====================================================================================+
            // If we are already moving item from/to hand, keep moving it
            if (OperationWithItem.CurrentState != OperationWithItem.State.Nothing)
                return (OperationWithItem.EquipItem() == OperationWithItem.State.Nothing); // If item finished moving return true

            // Read arg
            var item = World.Get(argList.NextAs<uint>());
            if (item == null)
                throw new ScriptRunTimeError(null, "item not found");
            var layer = (Layer)argList.NextAs<int>();
            if (layer == Layer.Invalid)
                layer = (Layer)ItemHold.ItemData.Layer;

            // ATTENTION: the logic of moving an item is used by several commands, so it was implemented as an operation
            OperationWithItem.EquipItem(item.Serial, layer);
            return OperationWithItem.CurrentState == OperationWithItem.State.Nothing;
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

        public static bool HeadMsg(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Never fails                                                                     |
            // +====================================================================================+
            // Read args and default to player serial
            var msg = argList.NextAs<string>();
            var color = argList.NextAs<ushort>();
            var serial = argList.NextAs<uint>();
            if (serial == 0)
                serial = World.Player.Serial;

            // Validate we have a valid serial
            var entity = World.Get(serial);
            if (entity == null)
                throw new ScriptCommandError("item or mobile not found");

            // Use same approach used when showing names in screen
            MessageManager.HandleMessage
            (
                null,
                msg,
                string.Empty,
                color,
                MessageType.Label,
                3,
                TextType.CLIENT
            );
            entity.AddMessage
            (
                MessageType.Label,
                msg,
                3,
                color,
                false,
                TextType.CLIENT
            );

            // Always succeeds
            return true;
        }

        public static bool SayMsg(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Simple (direct call) and never (ignore) fails                                   |
            // +====================================================================================+
            GameActions.Say(
            argList.NextAs<string>(),
            hue: argList.NextAs<ushort>()
            );
            return true;
        }

        internal static bool TimerMsg(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Simple (direct call) and never (ignore) fails                                   |
            // +====================================================================================+
            GameActions.Say(
            argList.NextAs<string>(),
            hue: argList.NextAs<ushort>()
            );
            return true;
        }

        internal static bool SayPartyMsg(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Process party commands                                                          |
            // +====================================================================================+
            // ATTENTION: Same logic performed by SystemChatControl, as if user typed in client
            var text = argList.NextAs<string>();
            switch (text.ToLower())
            {
                case "add":
                    if (World.Party.Leader == 0 || World.Party.Leader == World.Player)
                    {
                        GameActions.RequestPartyInviteByTarget();
                    }
                    else
                    {
                        MessageManager.HandleMessage
                        (
                            null,
                            ResGumps.YouAreNotPartyLeader,
                            "System",
                            0xFFFF,
                            MessageType.Regular,
                            3,
                            TextType.SYSTEM
                        );
                    }
                    break;
                case "loot":

                    if (World.Party.Leader != 0)
                    {
                        World.Party.CanLoot = !World.Party.CanLoot;
                    }
                    else
                    {
                        MessageManager.HandleMessage
                        (
                            null,
                            ResGumps.YouAreNotInAParty,
                            "System",
                            0xFFFF,
                            MessageType.Regular,
                            3,
                            TextType.SYSTEM
                        );
                    }
                    break;
                case "quit":
                    if (World.Party.Leader == 0)
                    {
                        MessageManager.HandleMessage
                        (
                            null,
                            ResGumps.YouAreNotInAParty,
                            "System",
                            0xFFFF,
                            MessageType.Regular,
                            3,
                            TextType.SYSTEM
                        );
                    }
                    else
                    {
                        GameActions.RequestPartyQuit();
                    }
                    break;
                case "accept":
                    if (World.Party.Leader == 0 && World.Party.Inviter != 0)
                    {
                        GameActions.RequestPartyAccept(World.Party.Inviter);
                        World.Party.Leader = World.Party.Inviter;
                        World.Party.Inviter = 0;
                    }
                    else
                    {
                        MessageManager.HandleMessage
                        (
                            null,
                            ResGumps.NoOneHasInvitedYouToBeInAParty,
                            "System",
                            0xFFFF,
                            MessageType.Regular,
                            3,
                            TextType.SYSTEM
                        );
                    }
                    break;
                case "decline":

                    if (World.Party.Leader == 0 && World.Party.Inviter != 0)
                    {
                        NetClient.Socket.Send(new PPartyDecline(World.Party.Inviter));
                        World.Party.Leader = 0;
                        World.Party.Inviter = 0;
                    }
                    else
                    {
                        MessageManager.HandleMessage
                        (
                            null,
                            ResGumps.NoOneHasInvitedYouToBeInAParty,
                            "System",
                            0xFFFF,
                            MessageType.Regular,
                            3,
                            TextType.SYSTEM
                        );
                    }
                    break;
                default:
                    if (World.Party.Leader != 0)
                    {
                        uint serial = 0;
                        int pos = 0;
                        while (pos < text.Length && text[pos] != ' ')
                        {
                            pos++;
                        }
                        if (pos < text.Length)
                        {
                            if (int.TryParse(text.Substring(0, pos), out int index) && index > 0 && index < 11 && World.Party.Members[index - 1] != null && World.Party.Members[index - 1].Serial != 0)
                            {
                                serial = World.Party.Members[index - 1].Serial;
                            }
                        }
                        GameActions.SayParty(text, serial);
                    }
                    else
                    {
                        GameActions.Print
                        (
                            string.Format(ResGumps.NoteToSelf0, text),
                            0,
                            MessageType.System,
                            3,
                            false
                        );
                    }
                    break;
            }
            return true;
        }

        internal static Command.Handler SayMsgType(MessageType type)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Simple (direct call) and never (ignore) fails                                   |
            // |  - Colors need to be respected                                                     |
            // +====================================================================================+

            // Using currying technique so method can be reused for all the different messaging commands
            return (argList, quiet, force) => {
 
                var msg = argList.NextAs<string>();

                // Select color based on msg type
                var color = ProfileManager.CurrentProfile.SpeechHue;
                switch (type)
                {
                    case MessageType.Whisper:
                        color = ProfileManager.CurrentProfile.WhisperHue;
                        break;
                    case MessageType.Emote:
                        msg = ResGeneral.EmoteChar + msg + ResGeneral.EmoteChar;
                        color = ProfileManager.CurrentProfile.EmoteHue;
                        break;
                    case MessageType.Yell:
                        color = ProfileManager.CurrentProfile.YellHue;
                        break;
                    case MessageType.Guild:
                        color = ProfileManager.CurrentProfile.GuildMessageHue;
                        break;
                }

                // Perform action of saying msg (sending pkg to server as well)
                GameActions.Say(
                msg,
                type: type,
                hue: color
                );
                return true;
            };
        }

        internal static Command.Handler PrintMsgType(MessageType type)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Simple (direct call) and never (ignore) fails                                   |
            // |  - Colors need to be respected                                                     |
            // +====================================================================================+

            // Using currying technique so method can be reused for all the different messaging commands
            return (argList, quiet, force) => {

                var msg = argList.NextAs<string>();

                // Select color based on msg type
                var color = ProfileManager.CurrentProfile.SpeechHue;

                // Perform action of saying msg (sending pkg to server as well)
                GameActions.Print(
                msg,
                type: type
                );
                return true;
            };
        }

        private static bool Info(ArgumentList argList, bool quiet, bool force)
        {
            CommandManager.Execute("info", "info");
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

        // ATTENTION: Expression implemented as a command to take advantage of the ArgumentList 
        private static bool FindAlias(ArgumentList argList, bool quiet, bool force)
        {
            var alias = argList.NextAs<string>().ToLower();
            uint value = 0;
            return Aliases.Read<uint>(alias, ref value);
        }

        private static LinkedListNode<Gump> WaitForGump_CurrentFirst = null;
        private static bool WaitForGump(ArgumentList argList, bool quiet, bool force)
        {
            var gumpid = argList.NextAs<uint>(); // GumpID is also a serial
            var timeout = argList.NextAs<int>(); // Timeout in ms

            if (WaitForGump_CurrentFirst == null)
            {
                WaitForGump_CurrentFirst = UIManager.Gumps.First;
                Interpreter.Timeout(timeout, () => {
                    WaitForGump_CurrentFirst = null;
                    return true;
                });
            }
            else
            {
                for (LinkedListNode<Gump> first = UIManager.Gumps.First; first != null; first = first.Next)
                {
                    Control c = first.Value;

                    if (!c.IsDisposed && c.ServerSerial == gumpid)
                    {
                        return true;
                    }
                }
            }
            return false;
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
        }

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

        private static bool PlaySound(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Name of sound does not need to be perfect match with .wav                       |
            // +====================================================================================+
            var sound = argList.NextAs<string>();
            int soundID;
            if (int.TryParse(sound, out soundID))
            {
                Client.Game.Scene.Audio.PlaySound(soundID);
            }
            else
            {
                for(int i = 0; i < Constants.MAX_SOUND_DATA_INDEX_COUNT; i++)
                {
                    Sound s = SoundsLoader.Instance.GetSound(i);
                    if (s != null && s.Name.Contains(sound))
                    {
                        Client.Game.Scene.Audio.PlaySound(s.Index);
                        break;
                    }
                    
                }
            }
            
            return true;
        }

        private static bool PlayMacro(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - IMPOSSIBLE TO MATCH BEHAVIOR - we don't have UO Steam Macros, but CUO Macros    |
            // +====================================================================================+
            MacroManager manager = Client.Game.GetScene<GameScene>().Macros;
            var macro = manager.FindMacro(argList.NextAs<string>());
            if (macro != null && macro.Items != null && macro.Items is MacroObject mac)
            {
                manager.SetMacroToExecute(mac);
                manager.WaitingBandageTarget = false;
                manager.WaitForTargetTimer = 0;
                manager.Update();
            }
            else
            {
                throw new ScriptCommandError("Macro not found, name is case sensitive");
            }

            return true;
        }

        private static bool ClearJournal(ArgumentList argList, bool quiet, bool force)
        {
            JournalManager.Entries.Clear();
            JournalGump journalGump = UIManager.GetGump<JournalGump>();
            if (journalGump != null)
            {
                var newJournal = new JournalGump { X = journalGump.X, Y = journalGump.Y };
                journalGump.Dispose();
                UIManager.Add(newJournal);
            }
            return true;
        }

        private static int WaitForJournal_Index = 0;
        private static bool WaitForJournal(ArgumentList argList, bool quiet, bool force)
        {
            var text = argList.NextAs<string>();
            var timeout = argList.NextAs<int>();
            var type = argList.NextAs<string>();

            if (WaitForJournal_Index == 0)
            {
                WaitForJournal_Index = JournalManager.Entries.Count - 1;
                Interpreter.Timeout(timeout, () => {
                    WaitForJournal_Index = 0;
                    return true;
                });
            }

            for(; WaitForJournal_Index < JournalManager.Entries.Count; WaitForJournal_Index++)
            {
                if(JournalManager.Entries[WaitForJournal_Index].Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    //if(type != string.Empty && JournalManager.Entries[WaitForJournal_Index].TextType == TextType.)
                    WaitForJournal_Index = 0;
                    Interpreter.ClearTimeout();
                    return true;
                }
            }

            return false;
        }

        ////public static bool ToggleScavenger(string command, ParameterList args)
        ////{
        ////    ScavengerAgent.Instance.ToggleEnabled();

        ////    return true;
        ////}



        private static bool Ping(ArgumentList argList, bool quiet, bool force)
        {
            NetClient.Socket.Send(new PPing());
            return true;
        }

        private static Gump messageBox_gump = null;
        private static bool MessageBox(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - MessageBox does not block script from running                                   |
            // |  - Consecutive calls replace the message box                                       |
            // +====================================================================================+
            //ATTENTION: No existing dependency to Windows Form, but we alreay use SDL. Unfortunatly, the behavior in UO Steam is to NOT block the game...
            //SDL.SDL_ShowSimpleMessageBox(SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_INFORMATION, argList.NextAs<string>(), argList.NextAs<string>(), IntPtr.Zero);
            // So we make a simple Gump instead
            var text = $"{argList.NextAs<string>()}-{argList.NextAs<string>()}";
            if (messageBox_gump != null)
                messageBox_gump.Dispose();

            var newMsgBox = new MacroButtonGump(new Macro(text), 50, 50);
            messageBox_gump = newMsgBox;
            UIManager.Add(newMsgBox);

            newMsgBox.SetInScreen();
            newMsgBox.BringOnTop();
            return true;
        }

        private static bool Paperdoll(ArgumentList argList, bool quiet, bool force)
        {
            // +== UOSTEAM =========================================================================+
            // |  - Beyond a basic double click as even animals get a bugged Paperdoll open         |
            // |  - Titles are not retrieved as it does not ping the server                         |
            // |  - Original doc states Serial as mandatory, but testing showed it was optional     |
            // +====================================================================================+
            // ATTENTION: same code of the Paperdoll pkg handling
            var serial = argList.NextAs<uint>();
            if (serial == 0)
                serial = World.Player.Serial;

            Mobile mobile = World.Mobiles.Get(serial);
            if (mobile == null)
                throw new ScriptCommandError("mobile not found");

            string text = (mobile.Title == string.Empty) ? mobile.Name : mobile.Title;
            mobile.Title = text;
            PaperDollGump paperdoll = UIManager.GetGump<PaperDollGump>(mobile);
            if (paperdoll == null)
            {
                if (!UIManager.GetGumpCachePosition(mobile, out Point location))
                {
                    location = new Point(100, 100);
                }

                UIManager.Add(new PaperDollGump(mobile, true) { Location = location });
            }
            else
            {
                bool old = paperdoll.CanLift;
                bool newLift = true;//(flags & 0x02) != 0;

                paperdoll.CanLift = newLift;
                paperdoll.UpdateTitle(text);

                if (old != newLift)
                {
                    paperdoll.RequestUpdateContents();
                }

                paperdoll.SetInScreen();
                paperdoll.BringOnTop();
            }
            return true;
        }

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

        private static bool CancelTarget(ArgumentList argList, bool quiet, bool force)
        {
            if (TargetManager.IsTargeting)
                TargetManager.CancelTarget();
            return true;
        }

        private static bool Where(ArgumentList argList, bool quiet, bool force)
        {
            GameActions.Print($"Location: {World.Player.X}, {World.Player.Y}, {World.Player.Z}");
            return true;
        }
        

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
