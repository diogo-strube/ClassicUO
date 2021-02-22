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
            Write<uint>("mount", GetLayerSerial(Data.Layer.Mount));
            Write<uint>("lefthand", GetHandSerial(IO.ItemExt_PaperdollAppearance.Left));
            Write<uint>("righthand", GetHandSerial(IO.ItemExt_PaperdollAppearance.Right));

            // Destination Aliases
            Write<uint>("ground", Ground);
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
    }
}