using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassicUO.Game.Scripting
{
    //ATTENTION: class only created because of issues when trying to guarantee behavior using GameActions.cs functionality
    internal static class OperationWithItem
    {
        public struct InteractingItem
        {
            public uint Serial;
            public uint Destination;
            public int OffsetX;
            public int OffsetY;
            public int OffsetZ;
            public int Amount;
            public Layer Layer;

            public InteractingItem(uint serial, uint destination = 0, int offsetX = 0, int offsetY = 0, int offsetZ = 0, int amount = 0, Layer layer = Layer.Invalid)
            {
                Serial = serial;
                Destination = destination;
                OffsetX = offsetX;
                OffsetY = offsetY;
                OffsetZ = offsetZ;
                Amount = amount;
                Layer = layer;
            }

            public InteractingItem(uint serial, uint destination, int offsetX, int offsetY, int offsetZ, int amount)
            {
                Serial = serial;
                Destination = destination;
                OffsetX = offsetX;
                OffsetY = offsetY;
                OffsetZ = offsetZ;
                Amount = amount;
                Layer = Layer.Invalid;
            }

            public InteractingItem(uint serial, Layer layer)
            {
                Serial = serial;
                Destination = 0;
                OffsetX = 0;
                OffsetY = 0;
                OffsetZ = 0;
                Amount = 0;
                Layer = layer;
            }
        };
        public static InteractingItem CurrentItem;

        public enum State
        {
            Nothing,
            Interacting
        }
        public static State CurrentState = State.Nothing;

        public static State MoveItem()
        {
            if(CurrentState != State.Nothing)
            {
                MoveItem(CurrentItem.Serial, CurrentItem.Destination, CurrentItem.OffsetX, CurrentItem.OffsetY, CurrentItem.OffsetZ, CurrentItem.Amount);
            }
            return CurrentState;
        }

        public static State MoveItem(uint serial, uint destination, int offsetX = 0xFFFF, int offsetY = 0xFFFF, int offsetZ = 0, int amount = 1)
        {
            // If trying to move anything different than the current moving item, block it
            if (CurrentState != State.Nothing && serial != CurrentItem.Serial)
            {
                throw new ScriptCommandError($"already moving {CurrentItem.Serial}");
            }

            // Pass over the state machine to decide what to do
            switch (CurrentState)
            {
                case State.Nothing: // If not moving, lets pick it up and start moving
                    if (ItemHold.Enabled)
                    {
                        GameActions.DropItem(ItemHold.Serial, ItemHold.X, ItemHold.Y, ItemHold.Z, destination);
                        throw new ScriptCommandError("You are already holding an item");
                    }
                    else
                    {
                        Interpreter.Timeout(5000, () => {
                            CurrentItem = new InteractingItem();
                            throw new ScriptCommandError("Timeout");
                        });

                        CurrentItem = new InteractingItem(serial, destination, offsetX, offsetY, offsetZ, amount);
                        Commands.DebugMsg($"Moving {serial} to {CurrentItem.Destination}");
                        GameActions.PickUp(CurrentItem.Serial, 0, 0, CurrentItem.Amount);
                        GameActions.DropItem(CurrentItem.Serial, CurrentItem.OffsetX, CurrentItem.OffsetY, CurrentItem.OffsetZ, CurrentItem.Destination);
                        //CurrentState = State.Interacting;
                        Interpreter.ClearTimeout();
                        CurrentState = State.Nothing;
                    }
                    break;
                //case State.Interacting: // lets wait for the item to be in the destination
                //    if (Commands.CmdFindEntityBySerial(CurrentItem.Serial, source: CurrentItem.Destination) != null)
                //    {
                //        Interpreter.ClearTimeout();
                //        CurrentItem = new InteractingItem();
                //        CurrentState = State.Nothing;
                //    }
                //    break;
            }
            return CurrentState;
        }

        public static State EquipItem()
        {
            if (CurrentState != State.Nothing)
            {
                EquipItem(CurrentItem.Serial, CurrentItem.Layer);
            }
            return CurrentState;
        }

        public static State EquipItem(uint serial, Layer layer)
        {
            // If trying to move anything different than the current moving item, block it
            if (CurrentState != State.Nothing && serial != CurrentItem.Serial)
            {
                throw new ScriptCommandError($"already moving {CurrentItem.Serial}");
            }

            // Pass over the state machine to decide what to do
            switch (CurrentState)
            {
                case State.Nothing: // If not moving, lets pick it up and start moving
                    if (ItemHold.Enabled)
                    {
                        GameActions.DropItem(ItemHold.Serial, ItemHold.X, ItemHold.Y, ItemHold.Z, ItemHold.Container);
                        throw new ScriptCommandError("You are already holding an item");
                    }
                    else
                    {
                        Interpreter.Timeout(5000, () => {
                            CurrentItem = new InteractingItem();
                            throw new ScriptCommandError("Timeout");
                        });

                        CurrentItem = new InteractingItem(serial, layer);
                        Commands.DebugMsg($"Moving {CurrentItem.Serial} to {CurrentItem.Layer.ToString()}");
                        GameActions.PickUp(CurrentItem.Serial, 0, 0, 1);
                        GameActions.Equip(CurrentItem.Layer);
                        //CurrentState = State.Interacting;
                        Interpreter.ClearTimeout();
                        CurrentState = State.Nothing;
                    }
                    break;
                //case State.Interacting: // lets wait for the item to be equipped
                //    if (World.Player.FindItemByLayer(CurrentItem.Layer) != null)
                //    {
                //        Interpreter.ClearTimeout();
                //        CurrentItem = new InteractingItem();
                //        CurrentState = State.Nothing;
                //    }
                //    break;
            }
            return CurrentState;
        }
    }
}
