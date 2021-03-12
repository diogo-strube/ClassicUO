using System;
using System.Collections.Generic;

namespace ClassicUO.Game.Scripting
{
    // An argument in the the Abstract Syntax Tree (AST)
    // For example (but not restricted to) the arguments provided to a given command in the UO Steam script
    public class Argument
    {
        // Node of the Abstract Syntax Tree
        protected ASTNode _node;

        // Execution state
        protected ScriptExecutionState _script;

        public Argument(ScriptExecutionState script, ASTNode node)
        {
            _script = script;
            _node = node;
        }

        // Generic method to interpreter an argument as the desired type T
        public virtual T As<T>()
        {
            if (_node.Lexeme == null)
                throw new ScriptRunTimeError(_node, "Cannot convert argument to " + typeof(T).FullName);

            // 1 - Try to resolve it as a scoped variable first
            var arg = _script.Lookup(_node.Lexeme);
            if (arg != null)
                return arg.As<T>();

            // 2 - Try to resolve it as a global alias (only if Serial)
            //if (_node.Type == ASTNodeType.SERIAL)
            //{
                T value = default(T);
                if (Aliases.Read<T>(_node.Lexeme.ToLower(), ref value))
                    return value;
            //}

            // 3 - If neither a scope variable or an alias, convert type based on Lexeme
            if(typeof(T) == typeof(string))
                return (T)Convert.ChangeType(_node.Lexeme, typeof(string));
            else return TypeConverter.To<T>(_node.Lexeme);
        }

        #region Comparison overrided methods
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

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        #endregion
    }

    // A virtualization of the Argument so that arguments may be created and injected in commands on the fly
    public class VirtualArgument : Argument
    {
        // Type of node represented by this virtual argument
        protected ASTNodeType _type;
        // Content of this virtual argument
        protected string _value;

        public VirtualArgument(string value, ASTNodeType type = ASTNodeType.STRING) : base(null, null)
        {
            _type = type;
            _value = value;
        }

        // Generic method to interpreter an argument as the desired type T
        public override T As<T>()
        {
            // Try to resolve it as a global alias (only if Serial)
            //if (_type == ASTNodeType.SERIAL)
            //{
            T value = default(T);
            if (typeof(T) == typeof(string) && value == null)
                value = (T)Convert.ChangeType(string.Empty, typeof(string));
            if (Aliases.Read<T>(_value, ref value))
                    return value;
            //}

            // If neither a scope variable or an alias, convert type based on stored value
            if (typeof(T) == typeof(string))
                return (T)Convert.ChangeType(_value, typeof(string));
            else return TypeConverter.To<T>(_value);
        }
    }

    // Encapsulate AST arguments so that command specific definitions can be evaluated and more easly processed
    // UOSTEAM: All returned strings are lower case, exatcly as UO Steam behavior. For example, alias "Scissors" is the same as "scissors"
    public class ArgumentList
    {
        // Code restricted to ArgumentList, and not in Aliases, as this is mapping value types beyond serial (color, source, etc)
        // Mapping a given value to another based on type (local type values are stored as objects are as a given arg type may not have multiple native types (like int, short, string, etc)
        private static Dictionary<string, Dictionary<object, object>> _argMap = new Dictionary<string, Dictionary<object, object>>();

        // Definition (name) for the type of each AST in the list
        private string[] _definitions;

        // All AST arguments provided to this operation (not necessarily a command)
        private Argument[] _args;

        // Index used to organize traversal of arguments
        private int _index;

        // Number of arguments that are mandatory
        private int _mandatory;

        public ArgumentList(Argument[] args, int mandatory, string[] definitions)
        {
            if (args.Length < mandatory)
                throw new ScriptSyntaxError("invalid number of arguments");

            _args = args;
            _mandatory = mandatory;
            _definitions = definitions;
            _index = -1;
        }

        // Array-like access interface
        public Argument this[int i]
        {

            get { _index = i; return _args[_index]; }
            set { _index = i; _args[_index] = value; }
        }

        // Array-like size
        public int Length
        {
            get { return _args.Length; }
        }


        // Generic method to read next argument in the list as the desired type T
        public T NextAs<T>()
        {
            var value = default(T);
            if (_args.Length > _index + 1) // Could we read one more argument?
            {
                // if anything other than a string, check mapped values first (as for example a ushort Color can be "any")
                if (typeof(T) != typeof(string))
                {
                    if (!GetMappedValue<T>(_definitions[++_index], value, ref value))
                        value = _args[_index].As<T>(); // After map is checked read argument as usual
                }
                else
                {   // otherwise read argument and later check value in map
                    value = _args[++_index].As<T>();
                    GetMappedValue<T>(_definitions[_index], value, ref value);
                } 
            }
            else if (_index + 1 >= _mandatory && _definitions.Length > _index + 1) // Nop, but this is optional, so go with default
                value = GetDefault<T>(_definitions[++_index]);
            else throw new ScriptSyntaxError(typeof(T).FullName + " argument does not exist at " + _index); // Otherwise.. kaboooom

            return value;
        }

        // Retrieve an arguments that contains and array of elements
        // Attention - inner values are not mapped to alias or local variables
        public T[] NextAsArray<T>()
        {
            if (_args.Length > _index + 1)
            {
                // Read array of elements and convert them to desired array type
                string arrayStr = _args[++_index].As<string>();
                string[] array = arrayStr.Replace(" ", "").Split(',');
                if (array.Length > 1)
                {
                    T[] value = new T[array.Length];
                    for (int i = 0; i < array.Length; i++)
                    {
                        value[i] = TypeConverter.To<T>(array[i]); // convert string to given type
                        GetMappedValue<T>(_definitions[_index], value[i], ref value[i]); // apply any existing map based on arg-type
                    }
                    return value;
                }
                else return new T[1] { TypeConverter.To<T>(array[0]) };
            }
            else if (_index + 1 >= _mandatory) // optional
                return new T[] { GetDefault<T>() }; // Return single element (Length == 1) for non existent optionals
            else throw new ScriptSyntaxError("List of " + typeof(T).FullName + " argument does not exist at " + _index);
        }  

        public T GetDefault<T>(string localAlias = "")
        {
            T value = default(T); // start with native default for types

            // but specilize as needed
            if (typeof(T) == typeof(int) && localAlias == "range")
                value = (T)Convert.ChangeType(int.MaxValue, typeof(int));

            // Than check mapped args
            GetMappedValue<T>(localAlias, value, ref value);

            return value;
        }

        #region Argument mapping method to add/remove/get
        private static bool GetMappedValue<T>(string argType, T argValue, ref T argDefault)
        {
            // if a string, make sure null is empty
            if (typeof(T) == typeof(string))
            {
                if(argValue == null)
                    argDefault = argValue = (T)Convert.ChangeType(string.Empty, typeof(string));
                else
                    argValue = (T)Convert.ChangeType(argValue.ToString().ToLower(), typeof(string));
            }            

            // check if in map and update value accordingly
            object obj = null;
            if (_argMap.ContainsKey(argType) && _argMap[argType].ContainsKey(argValue) && _argMap[argType].TryGetValue(argValue, out obj))
            {
                argDefault = (T)obj;
                return true;
            }
            else return false;
        }
        public static void AddMap<T>(string argType, T argValue, object argDefault)
        {
            if (!_argMap.ContainsKey(argType))
                _argMap.Add(argType, new Dictionary<object, object>());
            _argMap[argType].Add(argValue, argDefault);
        }
        public static void RemoveMap<T>(string argType, T argValue)
        {
            if (_argMap.ContainsKey(argType) && _argMap[argType].ContainsKey(argValue))
                _argMap[argType].Remove(argValue);
        }
        #endregion
    }
}
