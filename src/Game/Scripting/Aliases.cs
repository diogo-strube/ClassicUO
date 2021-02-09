using static ClassicUO.Game.Scripting.Interpreter;

namespace ClassicUO.Game.Scripting
{
    public static class Aliases
    {
        public static void Register()
        {
            Interpreter.RegisterAliasHandler<uint>("mount", Mount);

            Interpreter.SetAlias<ushort>("any", ushort.MaxValue); // mainly used for colors
        }

        private static bool Mount(string alias, out uint value)
        {
            var mount = World.Player.FindItemByLayer(Data.Layer.Mount);
            value = mount.Serial;
            return (mount!= null);
        }
    }
}