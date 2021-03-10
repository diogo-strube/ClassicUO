using ClassicUO.Game.Managers;
using System;
using System.Collections.Generic;
using static ClassicUO.Game.Scripting.Interpreter;

namespace ClassicUO.Game.Scripting
{
    // Links a Serial to any given word (ONLY FOR SERIAL)
    public static class Aliases
    {
        // Called by upper management class to register all the desired aliases
        public static void Register()
        {
            // Preregistered System Aliases
            Write<uint>("backpack", GetLayerSerial(Data.Layer.Backpack));
            Write<uint>("bank", GetLayerSerial(Data.Layer.Bank));
            Write<uint>("enemy", CurrentEnemy);
            Write<uint>("friend", CurrentFriend);
            Write<uint>("ground", Ground);
            Write<uint>("last", LastTarget);
            Write<uint>("lasttarget", LastTarget);
            Write<uint>("lastobject", LastObject);
            Write<uint>("lefthand", GetHandSerial(IO.ItemExt_PaperdollAppearance.Left));
            Write<uint>("mount", Mount);
            Write<uint>("righthand", GetHandSerial(IO.ItemExt_PaperdollAppearance.Right));
            Write<uint>("self", Self);
            Write<uint>("any", Any);
        }

        // Registry of global aliases mapping a name to a value
        // Stored based on type as an global alias is specific to a given spected type (for example, "any" color may be different from "any" range)
        // Global aliases can also be handled in runtime, as they map to a game variable
        private static Dictionary<Type, Dictionary<string, object>> _global = new Dictionary<Type, Dictionary<string, object>>();
        public delegate bool AliasHandler<T>(string alias, out T value);
        private static Dictionary<Type, Dictionary<string, object>> _globalHandlers = new Dictionary<Type, Dictionary<string, object>>();
        public static bool Read<T>(string alias, ref T value)
        {
            if (_globalHandlers.ContainsKey(typeof(T)) && _globalHandlers[typeof(T)].TryGetValue(alias, out object handlerObj))
            {
                AliasHandler<T> hander = (AliasHandler<T>)handlerObj;
                return hander(alias, out value);
            }
            else if (_global.ContainsKey(typeof(T)) && _global[typeof(T)].TryGetValue(alias, out object aliasObj))
            {
                value = (T)aliasObj;
                return true;
            }
            else return false;
        }
        public static void Write<T>(string alias, T value)
        {
            GameActions.Print("Object updated '" + alias + "' (" + value.ToString() + ")");
            if (!_global.ContainsKey(typeof(T)))
                _global.Add(typeof(T), new Dictionary<string, object>());
            if (!_global[typeof(T)].ContainsKey(alias))
                _global[typeof(T)].Add(alias, value);
            else _global[typeof(T)][alias] = value;
        }
        public static void Write<T>(string alias, AliasHandler<T> handler)
        {
            if(!_globalHandlers.ContainsKey(typeof(T)))
                _globalHandlers.Add(typeof(T), new Dictionary<string, object>());
            _globalHandlers[typeof(T)].Add(alias, handler);
        }
        public static void Remove(Type T, string alias)
        {
            if (_global.ContainsKey(T))
                _global[T].Remove(alias);
        }
        public static void Remove<T>(string keyword, AliasHandler<T> handler)
        {
            if (_globalHandlers.ContainsKey(typeof(T)))
                _globalHandlers[typeof(T)].Remove(keyword);
        }

        private static bool Mount(string alias, out uint value)
        {
            value = 0;
            // If player is mounted we retrieve the mount serial by Layer
            if (World.Player.IsMounted) 
            {
                var handler = GetLayerSerial(Data.Layer.Mount);
                if (handler("mount", out value))
                {
                    // If save the mount serial by value (not delegate) as well so it can be retrieved when unmounted
                    Write<uint>("mount", value);
                    return true;
                }
            }
            else if (_global.ContainsKey(typeof(uint)) && _global[typeof(uint)].TryGetValue(alias, out object aliasObj))
            {
                // Try retrieving serial by value (in case  player already unmounted or this was set via prompt/setalias)
                value = (uint)aliasObj;
                return true;
            }    
            return false;
        }

        // Get the Serial for the desired layer (using "curry" technique for param reduction)
        private static AliasHandler<uint> GetLayerSerial(Data.Layer layer)
        {
            return (string alias, out uint value) => {
                var item = World.Player.FindItemByLayer(layer);
                if(item != null)
                    value = item.Serial;
                else
                    value = 0;
                return true; // we always return true, as finding no item in hand does not mean alias was not found
            };
        }

        // Get the Serial for the item being hold ina  given hand (using "curry" technique for param reduction)
        private static AliasHandler<uint> GetHandSerial(IO.ItemExt_PaperdollAppearance hand)
        {
            return (string alias, out uint value) => {
                var item = World.Player.FindItemByHand(hand);
                if (item != null)
                    value = item.Serial;
                else
                    value = 0;
                return true; // we always return true, as finding no item in hand does not mean alias was not found
            };
           
        }

        private static bool Ground(string alias, out uint value)
        {
            value = uint.MaxValue; // Ground is MaxValue and Any is Zero
            return true;
        }

        private static bool CurrentEnemy(string alias, out uint value)
        {
            value = TargetManager.LastAttack;
            return true;
        }

        private static bool CurrentFriend(string alias, out uint value)
        {
            // TODO: Current game logic has no support for friends
            // We can add such logic in the TargetManager.
            value = 0;
            return true;
        }

        private static bool Self(string alias, out uint value)
        {
            value = World.Player.Serial;
            return true;
        }

        private static bool LastTarget(string alias, out uint value)
        {
            value = TargetManager.LastTargetInfo.Serial;
            return true;
        }

        private static bool LastObject(string alias, out uint value)
        {
            // TODO: Current game logic has no support for friends
            // We can add such logic in the TargetManager.
            value = 0;
            return true;
        }

        private static bool Any(string alias, out uint value)
        {
            // TODO: Current game logic has no support for friends
            // We can add such logic in the TargetManager.
            value = 0;
            return true;
        }
    }
}