using ClassicUO.Game.Data;
using ClassicUO.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ClassicUO.Game.Scripting
{
    // An argument provided to a given command in the UO Steam script
    public class Argument
    {
        private ASTNode _node;
        private ScriptExecutionState _script;

        public Argument(ScriptExecutionState script, ASTNode node)
        {
            _node = node;
            _script = script;
        }

        // Generic method to treat an argument as the desired type T
        public T As<T>()
        {
            if (_node.Lexeme == null)
                throw new ScriptRunTimeError(_node, "Cannot convert argument to " + typeof(T).FullName);

            // Try to resolve it as a scoped variable first
            var arg = _script.Lookup(_node.Lexeme);
            if (arg != null)
                return arg.As<T>();

            // Resolve it as a global alias next
            T value = default(T);
            if (Interpreter.GetAlias<T>(_node.Lexeme, ref value))
                return value;

            // If neither a scope variable or a global alias, convert type based on Lexeme
            if (typeof(T) == typeof(string)) return (T)Convert.ChangeType(_node.Lexeme, typeof(T));
            else return TypeConverter.To<T>(_node.Lexeme);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            Argument arg = obj as Argument;

            if (arg == null)
                return false;

            return Equals(arg);
        }

        public bool Equals(Argument other)
        {
            if (other == null)
                return false;

            return (other._node.Lexeme == _node.Lexeme);
        }
    }

    public class ArgumentList
    {
        // UOSteam acceptable directions
        public enum Directions : byte
        {
            North = (byte)Direction.North,
            Northeast = (byte)Direction.Right,
            Right = (byte)Direction.Right,
            East = (byte)Direction.East,
            Southeast = (byte)Direction.Down,
            Down = (byte)Direction.Down,
            South = (byte)Direction.South,
            Southwest = (byte)Direction.Left,
            Left = (byte)Direction.Left,
            West = (byte)Direction.West,
            Northwest = (byte)Direction.Up,
            Up = (byte)Direction.Up
        };

        // UOSteam acceptable sources
        public const Sources DefaultSource = Sources.Any;
        public enum Sources : byte
        {
            // Majority of Sources are low values and match the internal Layer enum
            Right = (byte)Layer.HeldInHand1,
            Righthand = (byte)Layer.HeldInHand1,
            Lefthand = (byte)Layer.HeldInHand2,
            Left = (byte)Layer.HeldInHand2,
            Shoes = (byte)Layer.Shoes,
            Pants = (byte)Layer.Pants,
            Shirt = (byte)Layer.Shirt,
            Head = (byte)Layer.Helmet,
            Helmet = (byte)Layer.Helmet,
            Gloves = (byte)Layer.Gloves,
            Ring = (byte)Layer.Ring,
            Talisman = (byte)Layer.Talisman,
            Neck = (byte)Layer.Necklace,
            Hair = (byte)Layer.Hair,
            Waist = (byte)Layer.Waist,
            Innertorso = (byte)Layer.Torso,
            Bracelet = (byte)Layer.Bracelet,
            Facialhair = (byte)Layer.Beard, // we are skipping Layer for Face - 15 (0x0F)
            Middletorso = (byte)Layer.Tunic,
            Earrings = (byte)Layer.Earrings,
            Arms = (byte)Layer.Arms,
            Cloak = (byte)Layer.Cloak,
            Backpack = (byte)Layer.Backpack,
            Outertorso = (byte)Layer.Robe,
            Outerlegs = (byte)Layer.Skirt,
            Innerlegs = (byte)Layer.Legs,
            Mount = (byte)Layer.Mount,
            Shopbuy = (byte)Layer.ShopBuy, // UO Steam documentation has Shop Buy as 26, but Layer enum has it as 27
            Shoprestock = (byte)Layer.ShopBuyRestock, // UO Steam documentation has Shop Restock as 27, but Layer enum has it as 26
            Shopsell = (byte)Layer.ShopSell,
            Bank = (byte)Layer.Bank,

            // A few sources are very high values and represent UO Steam concepts
            Ground = (byte)254,
            Any = (byte)255, // any is the default and equals to not providing a source
        };

        // UOSteam supported Hand options
        public const Hands DefaultHand = Hands.Right;
        public enum Hands : byte
        {
            // Hands mapping to the internal ItemExt_PaperdollAppearance layer
            Left = (byte)IO.ItemExt_PaperdollAppearance.Left,
            Right = (byte)IO.ItemExt_PaperdollAppearance.Right,
            // Concept of both hands
            Both = (byte)255
        };

        public enum ArgumentType
        {
            Mandatory,
            Optional,
        }
        public const uint DefaultSerial = 0;


        public const int DefaultAmount = 1;
        public const int DefaultRange = 2;
        public const ushort DefaultGraphic = 0;


        private Argument[] _args;
        private int _index;

        public ArgumentList(Argument[] args)
        {
            _args = args;
            _index = -1;
        }

        public Argument this[int i]
        {

            get { _index = i; return _args[_index]; }
            set { _index = i; _args[_index] = value; }
        }

        public int Length
        {
            get { return _args.Length; }
        }

        public bool TryAsSerial(out uint serial)
        {
            var tryIndex = _index + 1;
            try
            {
                serial = _args[tryIndex].As<uint>();
                _index = tryIndex;
                return true;
            }
            catch (ScriptRunTimeError ex)
            {
                serial = DefaultSerial;
                return false;
            }
        }

        public uint NextAsSerial(ArgumentType type = ArgumentType.Optional)
        {
            if (_args.Length > _index + 1)
                return _args[++_index].As<uint>();
            else if (type == ArgumentType.Optional)
                return DefaultSerial;
            else throw new ScriptRunTimeError(null, "Serial argument does not exist at " + _index);
        }

        public Sources NextAsSource(ArgumentType type = ArgumentType.Optional)
        {
            if (_args.Length > _index + 1)
            {
                return (Sources)Enum.Parse(typeof(Sources), _args[++_index].As<string>(), true);
            }
            else if (type == ArgumentType.Optional)
                return DefaultSource;
            else throw new ScriptRunTimeError(null, "Source argument does not exist at " + _index);
        }

        public bool TryAsGraphic(out uint graphic)
        {
            var tryIndex = _index + 1;
            try
            {
                graphic = _args[tryIndex].As<ushort>();
                _index = tryIndex;
                return true;
            }
            catch (ScriptRunTimeError ex)
            {
                graphic = DefaultGraphic;
                return false;
            }
        }

        public Hands NextAsHand(ArgumentType type = ArgumentType.Optional)
        {
            if (_args.Length > _index + 1)
                return (Hands)Enum.Parse(typeof(Hands), _args[++_index].As<string>(), true);
            // Optional is unsupported
            else throw new ScriptRunTimeError(null, "Hand argument does not exist at " + _index);
        }

        public List<Directions> NextAsDirection(ArgumentType type = ArgumentType.Optional)
        {
            if (_args.Length > _index + 1)
            {
                var movement = _args[++_index].As<string>();
                string[] path = movement.Split(',');
                List<Directions> dir = new List<Directions>();
                foreach (var node in path)
                {
                    dir.Add((Directions)Enum.Parse(typeof(Directions), node.Trim(), true));
                }
                return dir;
            }
            // Optional is unsupported
            else throw new ScriptRunTimeError(null, "Direction argument does not exist at " + _index);
        }

        public T NextAs<T>(ArgumentType type = ArgumentType.Optional)
        {
            if (_args.Length > _index + 1)
                return _args[++_index].As<T>();
            else if (type == ArgumentType.Optional)
                return GetDefault<T>();
            else throw new ScriptRunTimeError(null, typeof(T).FullName + " argument does not exist at " + _index);
        }

        // Retrieve an arguments that contains and array of elements
        public T[] NextAsArray<T>(ArgumentType type = ArgumentType.Optional)
        {
            if (_args.Length > _index + 1)
            {
                // Read array of elements and convert them to desired array type
                string arrayStr = _args[++_index].As<string>();
                string[] array = arrayStr.Replace(" ", "").Split(',');
                return Array.ConvertAll(array, new Converter<string, T>(TypeConverter.To<T>));
            }
            else if (type == ArgumentType.Optional)
                return new T[]{ GetDefault<T>() }; // Return single element (Length == 1) for non existent optionals
            else throw new ScriptRunTimeError(null, "List of " + typeof(T).FullName + " argument does not exist at " + _index);
        }
        
        // Retrieve the default value for a type T (numbers or string)
        public T GetDefault<T>()
        {
            // If a number the default is Max
            FieldInfo maxValueField = typeof(T).GetField("MaxValue", BindingFlags.Public | BindingFlags.Static);
            if (maxValueField != null)
                return (T)maxValueField.GetValue(null);
            // Otherwise it has to be string a string is "any"
            return (T)Convert.ChangeType("any", typeof(T));
        }
    }
}
