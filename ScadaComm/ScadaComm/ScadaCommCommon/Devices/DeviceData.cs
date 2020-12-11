﻿/*
 * Copyright 2020 Mikhail Shiryaev
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 * 
 * Product  : Rapid SCADA
 * Module   : ScadaCommCommon
 * Summary  : Represents device data
 * 
 * Author   : Mikhail Shiryaev
 * Created  : 2020
 * Modified : 2020
 */

using Scada.Data.Const;
using Scada.Data.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Scada.Comm.Devices
{
    /// <summary>
    /// Represents device data.
    /// <para>Представляет данные КП.</para>
    /// </summary>
    /// <remarks>The class is thread safe.</remarks>
    public class DeviceData
    {
        private readonly Queue<DeviceSlice> slices; // the archive data queue
        private readonly Queue<DeviceEvent> events; // the event queue
        private readonly object curDataLock;        // syncronizes access to current data

        private DeviceTags deviceTags; // the device tags
        private bool[] modifiedFlags;  // the tag modification flags
        private CnlData[] rawData;     // the raw tag data


        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public DeviceData()
        {
            slices = new Queue<DeviceSlice>();
            events = new Queue<DeviceEvent>();
            curDataLock = new object();

            deviceTags = null;
            modifiedFlags = null;
            rawData = null;
        }


        /// <summary>
        /// Gets or sets the data for the device tag at the specified index.
        /// </summary>
        public CnlData this[int tagIndex]
        {
            get
            {
                if (rawData == null)
                {
                    return CnlData.Empty;
                }
                else
                {
                    lock (curDataLock)
                    {
                        DeviceTag deviceTag = deviceTags[tagIndex];
                        return rawData[deviceTag.DataIndex];
                    }
                }
            }
            set
            {
                if (rawData != null)
                {
                    lock (curDataLock)
                    {
                        DeviceTag deviceTag = deviceTags[tagIndex];
                        modifiedFlags[tagIndex] = rawData[deviceTag.DataIndex] != value;
                        rawData[deviceTag.DataIndex] = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the data for the device tag at the specified code.
        /// </summary>
        public CnlData this[string tagCode]
        {
            get
            {
                if (rawData == null)
                {
                    return CnlData.Empty;
                }
                else
                {
                    lock (curDataLock)
                    {
                        DeviceTag deviceTag = deviceTags[tagCode];
                        return rawData[deviceTag.DataIndex];
                    }
                }
            }
            set
            {
                if (rawData != null)
                {
                    lock (curDataLock)
                    {
                        DeviceTag deviceTag = deviceTags[tagCode];
                        modifiedFlags[deviceTag.Index] = rawData[deviceTag.DataIndex] != value;
                        rawData[deviceTag.DataIndex] = value;
                    }
                }
            }
        }


        /// <summary>
        /// Initializes the device data to maintain the specified device tags.
        /// </summary>
        public void Init(DeviceTags deviceTags)
        {
            this.deviceTags = deviceTags ?? throw new ArgumentNullException(nameof(deviceTags));
            int dataLength = 0;

            foreach (DeviceTag deviceTag in deviceTags)
            {
                deviceTag.DataIndex = dataLength;
                dataLength += deviceTag.DataLength;
            }

            modifiedFlags = new bool[deviceTags.Count];
            rawData = new CnlData[dataLength];
        }

        /// <summary>
        /// Gets the floating point value of the tag.
        /// </summary>
        public double Get(int tagIndex)
        {
            CnlData cnlData = this[tagIndex];
            return cnlData.Stat > CnlStatusID.Undefined ? cnlData.Val : 0.0;
        }

        /// <summary>
        /// Gets the floating point value of the tag.
        /// </summary>
        public double Get(string tagCode)
        {
            CnlData cnlData = this[tagCode];
            return cnlData.Stat > CnlStatusID.Undefined ? cnlData.Val : 0.0;
        }

        /// <summary>
        /// Gets the integer value of the tag.
        /// </summary>
        public long GetInt64(int tagIndex)
        {
            CnlData cnlData = this[tagIndex];
            return cnlData.Stat > CnlStatusID.Undefined ? BitConverter.DoubleToInt64Bits(cnlData.Val) : 0;
        }

        /// <summary>
        /// Gets the integer value of the tag.
        /// </summary>
        public long GetInt64(string tagCode)
        {
            CnlData cnlData = this[tagCode];
            return cnlData.Stat > CnlStatusID.Undefined ? BitConverter.DoubleToInt64Bits(cnlData.Val) : 0;
        }

        /// <summary>
        /// Gets the array of floating point values.
        /// </summary>
        public double[] GetDoubleArray(int tagIndex)
        {
            lock (curDataLock)
            {
                int arrayLength = deviceTags[tagIndex].DataLength;
                double[] array = new double[arrayLength];

                for (int i = 0; i < arrayLength; i++)
                {
                    array[i] = Get(tagIndex++);
                }

                return array;
            }
        }

        /// <summary>
        /// Gets the array of floating point values.
        /// </summary>
        public double[] GetDoubleArray(string tagCode)
        {
            DeviceTag deviceTag = deviceTags[tagCode];
            return GetDoubleArray(deviceTag.Index);
        }

        /// <summary>
        /// Gets the array of bytes.
        /// </summary>
        public byte[] GetByteArray(int tagIndex)
        {
            double[] doubleArray = GetDoubleArray(tagIndex);
            int dataLength = doubleArray.Length * 8;
            byte[] byteArray = new byte[dataLength];
            Buffer.BlockCopy(doubleArray, 0, byteArray, 0, dataLength);
            return byteArray;
        }

        /// <summary>
        /// Gets the array of bytes.
        /// </summary>
        public byte[] GetByteArray(string tagCode)
        {
            DeviceTag deviceTag = deviceTags[tagCode];
            return GetByteArray(deviceTag.Index);
        }

        /// <summary>
        /// Gets the ASCII string value of the tag.
        /// </summary>
        public string GetAscii(int tagIndex)
        {
            byte[] array = GetByteArray(tagIndex);
            return Encoding.ASCII.GetString(array).TrimEnd((char)0);
        }

        /// <summary>
        /// Gets the ASCII string value of the tag.
        /// </summary>
        public string GetAscii(string tagCode)
        {
            DeviceTag deviceTag = deviceTags[tagCode];
            return GetAscii(deviceTag.Index);
        }

        /// <summary>
        /// Gets the Unicode string value of the tag.
        /// </summary>
        public string GetUnicode(int tagIndex)
        {
            byte[] array = GetByteArray(tagIndex);
            return Encoding.Unicode.GetString(array).TrimEnd((char)0);
        }

        /// <summary>
        /// Gets the Unicode string value of the tag.
        /// </summary>
        public string GetUnicode(string tagCode)
        {
            DeviceTag deviceTag = deviceTags[tagCode];
            return GetUnicode(deviceTag.Index);
        }

        /// <summary>
        /// Sets the floating point value and status of the tag.
        /// </summary>
        public void Set(int tagIndex, double val, int stat)
        {
            this[tagIndex] = new CnlData(val, stat);
        }

        /// <summary>
        /// Sets the floating point value of the tag.
        /// </summary>
        public void Set(int tagIndex, double val)
        {
            this[tagIndex] = double.IsNaN(val) ?
                CnlData.Empty :
                new CnlData(val, CnlStatusID.Defined);
        }

        /// <summary>
        /// Sets the floating point value and status of the tag.
        /// </summary>
        public void Set(string tagCode, double val, int stat)
        {
            this[tagCode] = new CnlData(val, stat);
        }

        /// <summary>
        /// Sets the floating point value of the tag.
        /// </summary>
        public void Set(string tagCode, double val)
        {
            this[tagCode] = double.IsNaN(val) ?
                CnlData.Empty :
                new CnlData(val, CnlStatusID.Defined);
        }

        /// <summary>
        /// Sets the integer value and status of the tag.
        /// </summary>
        public void SetInt64(int tagIndex, long val, int stat)
        {
            this[tagIndex] = new CnlData(BitConverter.Int64BitsToDouble(val), stat);
        }

        /// <summary>
        /// Sets the integer value and status of the tag.
        /// </summary>
        public void SetInt64(string tagCode, long val, int stat)
        {
            this[tagCode] = new CnlData(BitConverter.Int64BitsToDouble(val), stat);
        }

        /// <summary>
        /// Sets the array of floating point values and status starting from the specified tag.
        /// </summary>
        public void SetDoubleArray(int tagIndex, double[] vals, int stat)
        {
            lock (curDataLock)
            {
                DeviceTag deviceTag = deviceTags[tagIndex];
                int idx = deviceTag.DataIndex;
                int len = deviceTag.DataLength;
                int valLen = vals.Length;
                bool modified = false;

                for (int i = 0; i < len; i++)
                {
                    CnlData cnlData = new CnlData(i < valLen ? vals[i] : 0.0, stat);

                    if (rawData[idx] != cnlData)
                    {
                        rawData[idx] = cnlData;
                        modified = true;
                    }

                    idx++;
                }

                modifiedFlags[tagIndex] = modified;
            }
        }

        /// <summary>
        /// Sets the array of floating point values and status starting from the specified tag.
        /// </summary>
        public void SetDoubleArray(string tagCode, double[] vals, int stat)
        {
            DeviceTag deviceTag = deviceTags[tagCode];
            SetDoubleArray(deviceTag.Index, vals, stat);
        }

        /// <summary>
        /// Sets the array of bytes and status starting from the specified tag.
        /// </summary>
        public void SetByteArray(int tagIndex, byte[] vals, int stat)
        {
            int dataLength = vals.Length;
            int arrayLength = dataLength % 8 == 0 ? dataLength / 8 : dataLength / 8 + 1;
            double[] doubleArray = new double[arrayLength];
            Buffer.BlockCopy(vals, 0, doubleArray, 0, dataLength);
            SetDoubleArray(tagIndex, doubleArray, stat);
        }

        /// <summary>
        /// Sets the array of bytes and status starting from the specified tag.
        /// </summary>
        public void SetByteArray(string tagCode, byte[] vals, int stat)
        {
            DeviceTag deviceTag = deviceTags[tagCode];
            SetByteArray(deviceTag.Index, vals, stat);
        }

        /// <summary>
        /// Sets the ASCII string value and status of the tag.
        /// </summary>
        public void SetAscii(int tagIndex, string s, int stat)
        {
            SetByteArray(tagIndex, Encoding.ASCII.GetBytes(s ?? ""), stat);
        }

        /// <summary>
        /// Sets the ASCII string value and status of the tag.
        /// </summary>
        public void SetAscii(string tagCode, string s, int stat)
        {
            SetByteArray(tagCode, Encoding.ASCII.GetBytes(s ?? ""), stat);
        }

        /// <summary>
        /// Sets the Unicode string value and status of the tag.
        /// </summary>
        public void SetUnicode(int tagIndex, string s, int stat)
        {
            SetByteArray(tagIndex, Encoding.Unicode.GetBytes(s ?? ""), stat);
        }

        /// <summary>
        /// Sets the Unicode string value and status of the tag.
        /// </summary>
        public void SetUnicode(string tagCode, string s, int stat)
        {
            SetByteArray(tagCode, Encoding.Unicode.GetBytes(s ?? ""), stat);
        }

        /// <summary>
        /// Sets the device status tag.
        /// </summary>
        public void SetStatusTag(DeviceStatus status)
        {
            if (deviceTags.ContainsTag(CommUtils.StatusTagCode))
                Set(CommUtils.StatusTagCode, (double)status);
        }

        /// <summary>
        /// Adds the specified term to the tag value.
        /// </summary>
        public void Add(int tagIndex, double val)
        {
            Set(tagIndex, Get(tagIndex) + val);
        }

        /// <summary>
        /// Adds the specified term to the tag value.
        /// </summary>
        public void Add(string tagCode, double val)
        {
            Set(tagCode, Get(tagCode) + val);
        }

        /// <summary>
        /// Sets all tags to undefined.
        /// </summary>
        public void Invalidate()
        {
            Invalidate(0, deviceTags.Count);
        }

        /// <summary>
        /// Sets the specified tag to undefined.
        /// </summary>
        public void Invalidate(int tagIndex)
        {
            lock (curDataLock)
            {
                DeviceTag deviceTag = deviceTags[tagIndex];
                int idx = deviceTag.DataIndex;
                int len = deviceTag.DataLength;
                bool modified = false;

                for (int i = 0; i < len; i++)
                {
                    if (rawData[idx] != CnlData.Empty)
                    {
                        rawData[idx] = CnlData.Empty;
                        modified = true;
                    }

                    idx++;
                }

                modifiedFlags[tagIndex] = modified;
            }
        }

        /// <summary>
        /// Sets the specified tag to undefined.
        /// </summary>
        public void Invalidate(string tagCode)
        {
            DeviceTag deviceTag = deviceTags[tagCode];
            Invalidate(deviceTag.Index);
        }

        /// <summary>
        /// Sets the specified tag range to undefined.
        /// </summary>
        public void Invalidate(int tagIndex, int tagCount)
        {
            for (int i = 0; i < tagCount; i++)
            {
                Invalidate(tagIndex + i);
            }
        }

        /// <summary>
        /// Sets the specified tag range to undefined.
        /// </summary>
        public void Invalidate(string tagCode, int tagCount)
        {
            DeviceTag deviceTag = deviceTags[tagCode];
            Invalidate(deviceTag.Index, tagCount);
        }

        /// <summary>
        /// Gets a slice of the current data of all tags.
        /// </summary>
        public DeviceSlice GetCurrentData()
        {
            lock (curDataLock)
            {
                DeviceSlice deviceSlice = new DeviceSlice(DateTime.UtcNow, deviceTags.Count, rawData.Length);
                int tagIndex = 0;
                int dataIndex = 0;

                foreach (DeviceTag deviceTag in deviceTags)
                {
                    deviceSlice.DeviceTags[tagIndex] = deviceTag;

                    for (int i = 0, len = deviceTag.DataLength; i < len; i++)
                    {
                        deviceSlice.CnlData[dataIndex] = rawData[dataIndex];
                        dataIndex++;
                    }

                    tagIndex++;
                }

                Array.Clear(modifiedFlags, 0, modifiedFlags.Length);
                return deviceSlice;
            }
        }

        /// <summary>
        /// Gets a slice of the current data for modified tags.
        /// </summary>
        public DeviceSlice GetModifiedData()
        {
            lock (curDataLock)
            {
                int tagCount = 0;   // number of modified tags
                int dataLength = 0; // data length of modified tags

                foreach (DeviceTag deviceTag in deviceTags)
                {
                    if (modifiedFlags[deviceTag.Index])
                    {
                        tagCount++;
                        dataLength += deviceTag.DataLength;
                    }
                }

                DeviceSlice deviceSlice = new DeviceSlice(DateTime.UtcNow, tagCount, dataLength);
                int tagIndex = 0;
                int dataIndex = 0;

                foreach (DeviceTag deviceTag in deviceTags)
                {
                    if (modifiedFlags[deviceTag.Index])
                    {
                        modifiedFlags[deviceTag.Index] = false;
                        deviceSlice.DeviceTags[tagIndex] = deviceTag;

                        for (int i = 0, len = deviceTag.DataLength; i < len; i++)
                        {
                            deviceSlice.CnlData[dataIndex] = rawData[deviceTag.DataIndex + i];
                            dataIndex++;
                        }

                        tagIndex++;
                    }
                }

                return deviceSlice;
            }
        }

        /// <summary>
        /// Adds the archive slice to the queue.
        /// </summary>
        public void EnqueueSlice(DeviceSlice deviceSlice)
        {
            if (deviceSlice == null)
                throw new ArgumentNullException(nameof(deviceSlice));

            lock (slices)
            {
                slices.Enqueue(deviceSlice);
            }
        }

        /// <summary>
        /// Removes the archive slices from the queue and adds them to the desctination.
        /// </summary>
        public void DequeueSlices(List<DeviceSlice> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            lock (slices)
            {
                while (slices.Count > 0)
                {
                    DeviceSlice deviceSlice = slices.Dequeue();
                    destination.Add(deviceSlice);
                }
            }
        }

        /// <summary>
        /// Adds the device event to the queue.
        /// </summary>
        public void EnqueueEvent(DeviceEvent deviceEvent)
        {
            if (deviceEvent == null)
                throw new ArgumentNullException(nameof(deviceEvent));

            lock (events)
            {
                events.Enqueue(deviceEvent);
            }
        }

        /// <summary>
        /// Removes the device events from the queue and adds them to the desctination.
        /// </summary>
        public void DequeueEvents(List<DeviceEvent> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            lock (events)
            {
                while (events.Count > 0)
                {
                    DeviceEvent deviceEvent = events.Dequeue();
                    destination.Add(deviceEvent);
                }
            }
        }
    }
}
