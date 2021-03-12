using ClassicUO.Game.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicUO.Game.Scripting
{
    // Loosely typed abstraction of a UO Steam command
    // Class relies on delegates and static members for allowing implementation of commands as static methods instead of polymorphism
    // ATTENTION: delegates were prefered to keep look&feel with CUO source code. But complex commands have specializations
    public class Command
    {
        // Delegate to allow customization of how execution (both action logic and wait logic) are performed 
        public delegate bool Handler(ArgumentList argList, bool quiet, bool force);

        // Tracks the last execution for all commands (using ClassicUO.Time Tick)
        protected static Dictionary<string, uint> _executionsTracker = new Dictionary<string, uint>();
        public uint LastExec
        {
            get
            {
                if (!_executionsTracker.ContainsKey(Keyword)) // Store current time if command was never executed
                    _executionsTracker[Keyword] = ClassicUO.Time.Ticks;
                return _executionsTracker[Keyword];
            }
        }

        // Basic usage syntax (mainly used for error msgs)
        public string Usage { get; protected set; }

        // Keyword in the script syntax (used when routing commands)
        public string Keyword { get; protected set; }

        // Handler to perform command logic (action of the command)
        // ATTENTION: Use of delegates allow commands to be implemented without polymorphism if desired
        public Handler ExecutionLogic { get; protected set; }

        // Time (in ms/ticks) the command has to wait between executions
        public int WaitTime { get; protected set; }

        // Metadata of arguments to handle default values and command specific aliases
        public int MandatoryArgs { get; protected set; } // Number of mandatory args (always first values in provided argument array)
        public string[] ExpectedArgs { get; protected set; } // All expected arguments (mandatory and optional)

        // Protected constructor to be used in polymorphism (allowing specialized classes to customize inner logic)
        protected Command(string usage)
        {
            Usage = usage;

            // ATTENTION - parsing UO Steam usage strings to capture the command metadata is controversial...
            //   But this approach seems to facilitate a lot the engineer time around adding a new command as it allows 
            //   syntax checks and argument recovery to me automated.
            if (Usage.Count(c => c == ' ') > 0)
            {
                Keyword = usage.Substring(0, usage.IndexOf(' '));
                MandatoryArgs = 0;
                int a = 0;
                while ((a = Usage.IndexOf(" (", a)) != -1)
                {
                    a += " (".Length;
                    MandatoryArgs++;
                }
                ExpectedArgs = String.Join("", usage.Substring(usage.IndexOf(' ') + 1).Split('[', ']', '(', ')')).Split(' '); // keeping just names - same regex [\[\]\(\)]
            }
            else // Some commnads have no arguments at all, such as 'togglemounted'
            {
                Keyword = usage;
                MandatoryArgs = 0;
            }
        }

        // Public constructor to be used when adding commands via delegates
        public Command(string usage, Handler execLogic, int waitTime) : this(usage)
        {
            ExecutionLogic = execLogic;
            WaitTime = waitTime;
        }

        // Execute the command according to queing rules and provided logic
        public bool Execute(string command, Argument[] args, bool quiet, bool force)
        {
            // UOSTEAM: Basic coarse argument check (count) is the first thing done when command is executed so default usage is shown to user
            // Notice that if a command does not support quiet or force, it just ignore those flags
            if (args.Length < MandatoryArgs)
            {
                GameActions.Print(Usage);
                return true;
            }

            // Check if waiting is over (no blocking, we keep checking as Razor does: each time game loop call us)
            if (Time.Ticks - LastExec > WaitTime)
            {
                _executionsTracker[Keyword] = ClassicUO.Time.Ticks; // Store time of this execution (as the last execution) 
                try
                {   // ATTENTION: we create an ArgumentList to avoid extra processing of arguments for every call (such as when Wait is called several times)
                    ArgumentList argList = new ArgumentList(args, MandatoryArgs, ExpectedArgs);
                    return ExecutionLogic(argList, quiet, force); // Execute the command and do the magic
                }
                catch (ScriptCommandError cex)
                {
                    // A command error is valid and usual UO Steam behavior (like "item not found"), so we just outpout and consider a success
                    GameActions.Print(Keyword + ": " + cex.Message, type: MessageType.System);
                    return true;
                }
                catch (ScriptSyntaxError sex)
                {
                    // A syntax error indicates the command is not being used in the right way, so output Usage and we are good
                    GameActions.Print(Usage);
                    return true;
                }
                finally
                {
                    Interpreter.ClearTimeout(); // Always clear the timeout after a command ended execution
                }
            }
            else return false;
        }
    }
}
