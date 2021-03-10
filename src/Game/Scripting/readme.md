# UO Steam Support

Support for the UO Steam scripting language and the surrounding Profiles functionality.

## UO Steam Scripts Compatibility

To provide maximum compatibility, each UO Steam command was tested in different situation, such as holding another item, or moving the character. The behavior of the commands was reimplemented as-is, making sure ported scripts behave as expected.

This means that, even if we could implement ‘clearhands’ or ‘equipitem’ to work while another item is being dragged with the mouse, we are not allowing it so the original UO Steam behavior is reproduced.

## Integration with ClassicUO

While implementing the script commands we noticed some challenges to integrate it with existing ClassicUO logic. The majority of the issues were around the action’s queues (currently there are two separate queues for Dress and UseItem in the CUO logic) and how the game update/draw loop is required for mouse selection or item state updates.

Our approach for integration and implementation, including the mentioned challenges, was to avoid changing any CUO class/logic outside the added Scripting source code, making sure avoid support efforts in this Outlands fork.


## Design Overview

* Any time related comparison uses ClassicUO.Time instead of .Net DateTime or TimeSpan.
* Any String to Enum check or comparison uses Enum.Parse instead of a switch or if/else block.
* Methods pptional arguments are heavly used to support script commands optional arguments.
* For decreasing if/else chains, and as exceptions are already in use, methods have several exit points.

## TODO
* Move all the GameActions.Print debugging calls to a Log method that can be enabled/disabled by the user in the Script UI.
