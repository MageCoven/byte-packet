#nullable enable

using System.Collections.Generic;
using System;
using System.Text;

// NOTE: Consider changing all the lengts (string length, packet length, etc.) to UInt32 since they are never meant to be negative

namespace BytePacket {
    public class Packet {

        #region Properties
        public const int BUFFERSIZE = 1024;
        public int packetLength {
            get => _packetLength;
            set {
                _packetLength = value;
                
                // NOTE: It is likely that this can be moved to the GetData function as it is unlikely that there is a need for the buffer to have the packet length before GetData is called
                byte[] temp = BitConverter.GetBytes(packetLength);
                for (int i = 0; i < 4; i++) {
                    buffer[i] = temp[i];
                }
            }
        }
        public bool isFinished {
            get => packetLength == bufferLength || (packetLength < bufferLength && packetLength >= bufferLength-BUFFERSIZE);
        }
        public int bufferLength { get => buffer.Count; }
        private int _packetLength;
        private int bufferPointer = 4;
        private List<byte> buffer;
        #endregion

        #region Constructors
        public Packet() {
            buffer = new List<byte>();
            buffer.AddRange(BitConverter.GetBytes(0));
            packetLength = 0;
        }

        public Packet(byte[] data) {
            buffer = new List<byte>(data);
            byte[] temp = new byte[4];
            buffer.CopyTo(0, temp, 0, 4);
            packetLength = BitConverter.ToInt32(temp);
        }
        #endregion

        #region Methods
        #region Write

        // TODO: Change the name to something a bit more universal
        public void WriteFromRead(byte[] data) {
            if (isFinished) {
                throw new Exception("Packets are not meant to be written to once they are finished");
            }

            buffer.AddRange(data);
        }

        private void SignElement(DataType type) {
            buffer.Add((byte)type);
            packetLength += 1;
        }

        public void Write(int _int) {
            SignElement(DataType.Int);
            buffer.AddRange(BitConverter.GetBytes(_int));
            packetLength += 4; // The 4 is because int32 is 4 bytes
        }

        public void Write(int[] intArray) {
            SignElement(DataType.IntArray);
            buffer.AddRange(BitConverter.GetBytes(intArray.Length));
            packetLength += 4; // The 4 is because int32 is 4 bytes

            foreach (int _int in intArray) {
                buffer.AddRange(BitConverter.GetBytes(_int));
            }
            packetLength += 4 * intArray.Length; // The 4 is because int32 is 4 bytes
        }

        public void Write(string _string) {
            // TODO: Make a const or override or something of the sort that enables the change from UTF-8 to UTF-16 for more non ASCII heavy languages
            SignElement(DataType.String);
            byte[] temp = Encoding.UTF8.GetBytes(_string);
            buffer.AddRange(BitConverter.GetBytes(temp.Length));
            buffer.AddRange(temp);
            packetLength += temp.Length + 4; // The 4 is because int32 is 4 bytes
        }

        public void Write(string[] stringArray) {
            SignElement(DataType.StringArray);
            buffer.AddRange(BitConverter.GetBytes(stringArray.Length));
            packetLength += 4; // The 4 is because int32 is 4 bytes

            foreach (string _string in stringArray) {
                byte[] temp = Encoding.UTF8.GetBytes(_string);
                buffer.AddRange(BitConverter.GetBytes(temp.Length));
                buffer.AddRange(temp);
                packetLength += temp.Length + 4; // The 4 is because int32 is 4 bytes
            }
        }
        #endregion

        #region Read
        private bool IsElementType(DataType type) {
            if ((DataType)buffer[bufferPointer] != type) {
                return false;
            }
            bufferPointer += 1;
            return true;
        }

        private byte[] ReadRaw(int length) {
            byte[] temp = new byte[length];
            buffer.CopyTo(bufferPointer, temp, 0, length);
            bufferPointer += length;
            return temp;
        }

        public ReadResult<int> ReadInt() {
            if (!IsElementType(DataType.Int)) {
                return new ReadResult<int>(0, ReadResultError.IncorrectType);
            }

            return new ReadResult<int>(BitConverter.ToInt32(ReadRaw(4), 0), null);
        }

        public ReadResult<int[]> ReadIntArray() {
            if (!IsElementType(DataType.IntArray)) {
                return new ReadResult<int[]>(new int[0], ReadResultError.IncorrectType);
            }
            
            int length = BitConverter.ToInt32(ReadRaw(4), 0);
            int[] arr = new int[length];
            for (int i = 0; i < length; i++) {
                arr[i] = BitConverter.ToInt32(ReadRaw(4), 0);
            }
            
            return new ReadResult<int[]>(arr, null);
        }

        public ReadResult<string> ReadString() {
            if (!IsElementType(DataType.String)) {
                return new ReadResult<string>("", ReadResultError.IncorrectType);
            }

            int stringByteLength = BitConverter.ToInt32(ReadRaw(4));
            return new ReadResult<string>(Encoding.UTF8.GetString(ReadRaw(stringByteLength)), null);
        }

        public ReadResult<string[]> ReadStringArray() {
            if (!IsElementType(DataType.StringArray)) {
                return new ReadResult<string[]>(new string[0], ReadResultError.IncorrectType);
            }
            
            int length = BitConverter.ToInt32(ReadRaw(4), 0);
            string[] arr = new string[length];
            for (int i = 0; i < length; i++) {
                int stringByteLength = BitConverter.ToInt32(ReadRaw(4));
                arr[i] = Encoding.UTF8.GetString(ReadRaw(stringByteLength));
            }

            return new ReadResult<string[]>(arr, null);
        }
        #endregion

        #region General
        public byte[] GetData() {
            if (buffer.Count % BUFFERSIZE != 0) {
                buffer.AddRange(new byte[BUFFERSIZE - buffer.Count % BUFFERSIZE]);
            }

            return buffer.ToArray();
        }

        public DataType? GetElementType() {
            if (!Enum.IsDefined(typeof(DataType), buffer[bufferPointer])) { return null; }
            return (DataType)buffer[bufferPointer];
        }
        #endregion
        #endregion

        #region DataTypes
        public enum DataType : byte {
            Int,
            IntArray,
            String,
            StringArray
        }

        // NOTE: This might not even be useful, but I thought of it so it's here now
        public static Type? GetTypeFromElementType(DataType type) {
            switch (type) {
                case DataType.Int:         return typeof(int);
                case DataType.IntArray:    return typeof(int[]);
                case DataType.String:      return typeof(string);
                case DataType.StringArray: return typeof(string[]);
            }

            return null;
        }
        #endregion

        #region ReadResult
        public struct ReadResult<T> {
            public readonly T value;
            public readonly ReadResultError? error;

            public ReadResult(T value, ReadResultError? error) {
                this.value = value;
                this.error = error;
            }
        }

        public enum ReadResultError {
            IncorrectType
        }
        #endregion
    }
}