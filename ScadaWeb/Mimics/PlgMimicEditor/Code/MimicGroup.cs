﻿// Copyright (c) Rapid Software LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Scada.Web.Plugins.PlgMimicEditor.Code
{
    /// <summary>
    /// Represents a group of mimic diagrams belonging to the same project.
    /// <para>Представляет группу мнемосхем, принадлежащих одному проекту.</para>
    /// </summary>
    public class MimicGroup
    {
        private readonly Dictionary<string, MimicInstance> mimicInstances;


        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public MimicGroup()
        {
            mimicInstances = [];
        }


        /// <summary>
        /// Gets the group name.
        /// </summary>
        public string Name { get; init; }


        /// <summary>
        /// Adds the mimic to the group.
        /// </summary>
        public void AddMimic(MimicInstance mimicInstance)
        {
            lock (mimicInstances)
            {
                mimicInstances.Add(mimicInstance.FileName, mimicInstance);
            }
        }

        /// <summary>
        /// Gets a snapshot containing the mimics of the group.
        /// </summary>
        public MimicInstance[] GetMimics()
        {
            lock (mimicInstances)
            {
                return [.. mimicInstances.Values.OrderBy(m => m.FileName)];
            }
        }
    }
}
