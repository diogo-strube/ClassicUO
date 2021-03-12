using ClassicUO.Game.GameObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassicUO.Game.Scripting
{
    internal static class OperationMoveItem
    {
        public struct MovingItem
        {
            public uint Serial;
            public uint Destination;
            public int OffsetX;
            public int OffsetY;
            public int OffsetZ;
            public int Amount;

            public MovingItem(uint serial, uint destination, int offsetX, int offsetY, int offsetZ, int amount)
            {
                Serial = serial;
                Destination = destination;
                OffsetX = offsetX;
                OffsetY = offsetY;
                OffsetZ = offsetZ;
                Amount = amount;
            }
        };
        public static MovingItem CurrentItem;

        public enum State
        {
            NotMoving,
            Moving,
        }
        public static State CurrentState = State.NotMoving;

        public static State MoveItem(uint serial, uint destination, int offsetX = 0xFFFF, int offsetY = 0xFFFF, int offsetZ = 0, int amount = 1)
        {
            // If trying to move anything different than the current moving item, block it
            if (CurrentState != State.NotMoving && serial != CurrentItem.Serial)
            {
                throw new ScriptCommandError($"already moving {CurrentItem.Serial}");
            }

            // Pass over the state machine to decide what to do
            switch (CurrentState)
            {
                case State.NotMoving: // If not moving, lets pick it up and start moving
                    if (ItemHold.Enabled)
                    {
                        GameActions.DropItem(ItemHold.Serial, ItemHold.X, ItemHold.Y, ItemHold.Z, destination);
                        throw new ScriptCommandError("You are already holding an item");
                    }
                    else
                    {
                        Interpreter.Timeout(5000, () => {
                            CurrentItem = new MovingItem();
                            throw new ScriptCommandError("Timeout");
                        });

                        CurrentItem = new MovingItem(serial, destination, offsetX, offsetY, offsetZ, amount);
                        Commands.DebugMsg($"Moving {serial}");
                        GameActions.PickUp(CurrentItem.Serial, 0, 0, CurrentItem.Amount);
                        GameActions.DropItem(CurrentItem.Serial, CurrentItem.OffsetX, CurrentItem.OffsetY, CurrentItem.OffsetZ, CurrentItem.Destination);
                        CurrentState = State.Moving;
                    }
                    break;
                case State.Moving: // If already moving, lets wait for the item to be in the destination
                    if (Commands.CmdFindEntityBySerial(CurrentItem.Serial, source: CurrentItem.Destination) != null)
                    {
                        Interpreter.ClearTimeout();
                        CurrentItem = new MovingItem();
                        CurrentState = State.NotMoving;
                    }
                    break;
            }
            return CurrentState;
        }
    }
}
