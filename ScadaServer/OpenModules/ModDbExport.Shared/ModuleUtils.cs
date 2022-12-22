﻿// Copyright (c) Rapid Software LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Scada.Server.Modules.ModDbExport
{
    /// <summary>
    /// The class provides helper methods for the module.
    /// <para>Класс, предоставляющий вспомогательные методы для модуля.</para>
    /// </summary>
    internal static class ModuleUtils
    {
        /// <summary>
        /// The module code.
        /// </summary>
        public const string ModuleCode = "ModDbExport";

        /// <summary>
        /// Calculates the next timer firing.
        /// </summary>
        public static DateTime CalcNextTimer(DateTime nowDT, int period)
        {
            return period > 0
                ? nowDT.Date.AddSeconds(((int)nowDT.TimeOfDay.TotalSeconds / period + 1) * period)
                : nowDT;
        }
    }
}
