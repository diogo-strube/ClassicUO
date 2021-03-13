# UO Steam Support

Support for the UO Steam scripting language and the surrounding Profiles functionality. Functionality implemented inside the CUO client, and not in a Plugin, to bring the best experience to the UO Outlands server. Nevertheless, implementation is encapsulated so that it may be easily moved to a Plugin if desired.

## UO Steam Scripts Compatibility

To provide maximum compatibility, each UO Steam command was tested in different situation, such as holding another item, moving the character, etc. The behavior of the commands was reimplemented as-is, making sure ported scripts behave as expected.

This means that, even if we could enhance the functionality (such as making commands like ‘clearhands’ or ‘equipitem’ to work while another item is being dragged with the mouse), we are not doing it so that the original UO Steam behavior is reproduced.

## Integration with ClassicUO

While implementing the script commands we noticed some challenges to integrate it with existing ClassicUO logic. The majority of the issues were around the action’s queues (currently there are two separate queues for Dress and UseItem in the CUO logic) and how the game update/draw loop is required for mouse selection or item state updates.

Our approach for integration and implementation, including the mentioned challenges, was to avoid changing any CUO class/logic outside the added Scripting source code, making sure avoid support efforts in this Outlands fork.

## Implementation Overview

### Code Standards

The following code standards and approaches were used when developing the UO Steam feature:
- Line limit set to 120.
- A few tags are used in the source code to explain specifics about the implementation:
  - UOSTEAM – describe implementation details done to match specific behavior in UO Steam. Therefore, changes may affect final desired behavior.
  - ATTENTION - Highlight’s and explain parts of the code that may be complex or have a controversial approach.
- Any time related comparison uses ClassicUO.Time instead of .Net DateTime or TimeSpan.
- Any String to Enum check or comparison uses Enum.Parse instead of a switch or if/else block.
- Methods optional arguments are heavly used to support script commands optional arguments.
- Aiming to decrease if/else chains, and as Exceptions are already in use, methods have several exit points.

## Behavior and Validation
Here is the table of validated commands inside UO Outlands Test Server (Published 3.0.7608.38527 and Manifest 2.8.7.188):

|     Command     | Moving | WithTarget | UsingItem |           Memory (aliases)             |
| --------------- | ------ | ---------- | --------- | -------------------------------------- |
|   togglehands   |  Yes   |    Yes     | Affected  | lastrightequipped, lastleftequipped    |
|  togglemounted  |  Yes   |    Yes     |    Yes    | mount                                  |
|    clearhands   |  Yes   |    Yes     | Affected  | lastrightequipped, lastleftequipped    |
|     moveitem    |  Yes   |    Yes     | Affected  |                                        |
| moveitemoffset  |  Yes   |    Yes     | Affected  |                                        |
|     movetype    |  Yes   |    Yes     | Affected  |                                        |
| movetypeoffset  |  Yes   |    Yes     | Affected  |                                        |

Cooldown timers are shared as follows:
|    Timer     |                               Commands                                    |
| ------------ | ------------------------------------------------------------------------- |
| PickingTimer | clearhands