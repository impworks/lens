using System;

namespace Lens.Compiler
{
    /// <summary>
    /// A predefined subsystem for easier safe mode tweaking.
    ///
    /// Each flag expands into namespace, type and member rules, and is read the way the explicit
    /// lists are: denied under a blacklist, allowed under a whitelist. What each one covers, and
    /// what no rule of any kind is allowed to hand back, is in SafeModeRules.
    /// </summary>
    [Flags]
    public enum SafeModeSubsystem
    {
        None = 0,
        Network = 0x001,
        IO = 0x002,
        Reflection = 0x004,
        Threading = 0x010,
        Environment = 0x020,

        /// <summary>
        /// Every subsystem above. Under a whitelist this is the widest the flags can make it, and
        /// under a blacklist the narrowest.
        /// </summary>
        All = Network | IO | Reflection | Threading | Environment
    }
}