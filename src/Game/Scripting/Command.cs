using ClassicUO.Game.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicUO.Game.Scripting
{
    // Defines the group a command i s part of based on performed actions
    public enum CommandGroup
    {
        None,   // This command has no blocking or time controlled operation
        PickUp, // This command will be performing a PickUp operation
        DClick  // This command will be performing a DClick operation
    }

    // Loosely typed abstraction of a UO Steam command
    // Class relies on delegates and static members for allowing implementation of commands as static methods instead of polymorphism
    // ATTENTION: delegates were prefered to keep look&feel with CUO source code. But complex commands have specializations
    public class Command
    {
        // Delegate to allow customization of how execution (both action logic and wait logic) are performed 
        public delegate bool Handler(ArgumentList argList, bool quiet, bool force);

        // Tracks the last execution for all commands (using ClassicUO.Time Tick)
        protected static Dictionary<string, uint> _cmdExecutionsTracker = new Dictionary<string, uint>();   // for this command
        protected static Dictionary<CommandGroup, uint> _groupExecutionsTracker = new Dictionary<CommandGroup, uint>(); // for the command groups
        public uint LastCmdExec
        {
            get
            {
                if (!_cmdExecutionsTracker.ContainsKey(Keyword)) // Store current time if command was never executed
                    _cmdExecutionsTracker[Keyword] = ClassicUO.Time.Ticks;
                return _cmdExecutionsTracker[Keyword];
            }
        }

        public uint LastGroupExec
        {
            get
            {
                if (!_groupExecutionsTracker.ContainsKey(Group)) // Store current time if no command in the group was ever executed
                    _groupExecutionsTracker[Group] = ClassicUO.Time.Ticks;
                return _groupExecutionsTracker[Group];
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

        // Group (behavior) for this command
        public CommandGroup Group { get; protected set; }

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
        public Command(string usage, Handler execLogic, int waitTime, CommandGroup group) : this(usage)
        {
            ExecutionLogic = execLogic;
            WaitTime = waitTime;
            Group = group;
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
            if (Wait())
            {   
                try
                {   // ATTENTION: we create an ArgumentList to avoid extra processing of arguments for every call (such as when Wait is called several times)
                    ArgumentList argList = new ArgumentList(args, MandatoryArgs, ExpectedArgs);
                    if(ExecutionLogic(argList, quiet, force)) // Execute the command and do the magic
                    {
                        Tick(); // ATTENTION: Store time of this execution (as the last execution) only when returning true
                        return true;
                    } 
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
                    Tick(); // ATTENTION: Store time of this execution (as the last execution) only when returning true
                    Interpreter.ClearTimeout(); // Always clear the timeout after a command ended execution
                }
            }
            return false;
        }

        // Check if the command needs to wait before executing
        protected bool Wait()
        {
            // Start checking the last time this command executed
            if (Time.Ticks - LastCmdExec > WaitTime)
            {
                // ATTENTION: as there are restrictions on action, like picking up an item, commands need shared waits as well
                // And than check other commands in the same bucket
                return (Time.Ticks - LastGroupExec > WaitTime);
            }
            return false;
        }

        // Update timers related to this command
        protected void Tick()
        {
            _cmdExecutionsTracker[Keyword] = ClassicUO.Time.Ticks;
            _groupExecutionsTracker[Group] = ClassicUO.Time.Ticks;
        }
    }
}
