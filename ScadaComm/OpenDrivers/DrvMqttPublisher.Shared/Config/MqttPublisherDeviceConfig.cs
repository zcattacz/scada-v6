﻿// Copyright (c) Rapid Software LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Scada.Comm.Lang;
using Scada.Config;
using System.Xml;

namespace Scada.Comm.Drivers.DrvMqttPublisher.Config
{
    /// <summary>
    /// Represents a configuration of an MQTT publisher device.
    /// <para>Представляет конфигурацию устройства для публикации данных с помощью MQTT.</para>
    /// </summary>
    internal class MqttPublisherDeviceConfig : BaseConfig
    {
        /// <summary>
        /// Gets the device options.
        /// </summary>
        public DeviceOptions DeviceOptions { get; private set; }

        /// <summary>
        /// Gets the items.
        /// </summary>
        public List<ItemConfig> Items { get; private set; }


        /// <summary>
        /// Sets the default values.
        /// </summary>
        protected override void SetToDefault()
        {
            DeviceOptions = new DeviceOptions();
            Items = new List<ItemConfig>();
        }

        /// <summary>
        /// Loads the configuration from the specified reader.
        /// </summary>
        protected override void Load(TextReader reader)
        {
            XmlDocument xmlDoc = new();
            xmlDoc.Load(reader);
            XmlElement rootElem = xmlDoc.DocumentElement;

            if (rootElem.SelectSingleNode("DeviceOptions") is XmlNode deviceOptionsNode)
                DeviceOptions.LoadFromXml(deviceOptionsNode);

            if (rootElem.SelectSingleNode("Items") is XmlNode itemsNode)
            {
                foreach (XmlElement itemElem in itemsNode.SelectNodes("Item"))
                {
                    ItemConfig itemConfig = new();
                    itemConfig.LoadFromXml(itemElem);
                    Items.Add(itemConfig);
                }
            }
        }

        /// <summary>
        /// Saves the configuration to the specified writer.
        /// </summary>
        protected override void Save(TextWriter writer)
        {
            XmlDocument xmlDoc = new();
            XmlDeclaration xmlDecl = xmlDoc.CreateXmlDeclaration("1.0", "utf-8", null);
            xmlDoc.AppendChild(xmlDecl);

            XmlElement rootElem = xmlDoc.CreateElement("MqttPublisherDeviceConfig");
            xmlDoc.AppendChild(rootElem);

            DeviceOptions.SaveToXml(rootElem.AppendElem("DeviceOptions"));
            XmlElement itemsElem = rootElem.AppendElem("Items");

            foreach (ItemConfig itemConfig in Items)
            {
                itemConfig.SaveToXml(itemsElem.AppendElem("Item"));
            }

            xmlDoc.Save(writer);
        }

        /// <summary>
        /// Builds an error message for the load operation.
        /// </summary>
        protected override string BuildLoadErrorMessage(Exception ex)
        {
            return ex.BuildErrorMessage(CommPhrases.LoadDeviceConfigError);
        }

        /// <summary>
        /// Builds an error message for the save operation.
        /// </summary>
        protected override string BuildSaveErrorMessage(Exception ex)
        {
            return ex.BuildErrorMessage(CommPhrases.SaveDeviceConfigError);
        }

        /// <summary>
        /// Gets the short name of the device configuration file.
        /// </summary>
        public static string GetFileName(int deviceNum)
        {
            return $"{DriverUtils.DriverCode}_{deviceNum:D3}.xml";
        }
    }
}