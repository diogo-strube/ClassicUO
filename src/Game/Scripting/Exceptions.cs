using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassicUO.Game.Scripting
{
    // Generic exception for the script functionality
    public class ScriptRunTimeError : Exception
    {
        public ASTNode Node;

        public ScriptRunTimeError(ASTNode node, string error) : base(error)
        {
            Node = node;
        }

        public ScriptRunTimeError(ASTNode node, string error, Exception inner) : base(error, inner)
        {
            Node = node;
        }
    }

    // Script exception related to calling a command with the wrong syntax (most valuable to player and UI feedback)
    public class ScriptSyntaxError : ScriptRunTimeError
    {
        public ScriptSyntaxError(string error, ScriptRunTimeError inner = null) : base(inner?.Node, error, inner)
        {
        }
    }

    // Script exception related to conversion issues between types, enums, dictionaries, etc
    public class ScriptTypeConversionError : ScriptRunTimeError
    {
        public ScriptTypeConversionError(ASTNode node, string error) : base(node, error)
        {
        }
    }

    // Command internal execution exception, such as when an item is not found
    public class ScriptCommandError : ScriptRunTimeError
    {
        public ScriptCommandError(string error) : base(null, error)
        {
        }
    }
}
