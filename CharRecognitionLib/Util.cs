using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharRecognitionLib
{
    public class Util
    {
        /// <summary>
        /// Gain access to the Azure storage blob
        /// </summary>
        static BlobClient get_Azure_blob(string container, string blob_name)
        {
            var service_client = new BlobServiceClient(Credentials.Azure_Blob_Storage);
            var container_client = service_client.GetBlobContainerClient(container);
            var blob = container_client.GetBlobClient(blob_name);

            return blob;
        }


        /// <summary>
        /// If the "blob_name" does not exist, return a null
        /// </summary>
        public static byte[] Download_From_Storage(string container_name,
            string blob_name)
        {
            var blob = get_Azure_blob(container_name, blob_name);

            if (blob.Exists())
                return blob.DownloadContent().Value.Content.ToArray();
            else
                return null;
        }


        public static void Upload_To_Storage(string container_name,
            string blob_name, byte[] bytes)
        {
            var blob = get_Azure_blob(container_name, blob_name);
            blob.Upload(new BinaryData(bytes), overwrite: true);
        }


        public static void Upload_To_Storage_Async(string container_name,
            string blob_name, byte[] bytes)
        {
            var blob = get_Azure_blob(container_name, blob_name);
            blob.UploadAsync(new BinaryData(bytes), overwrite: true);
        }
    }



    /// <summary>
    /// A class for writing a List of bytes.
    /// </summary>
    class BytesWriter
    {
        List<byte> bytes = new List<byte>();

        public List<byte> BytesList
        {
            get { return bytes; }
        }

        public void Add_Int(int value)
        {
            bytes.AddRange(BitConverter.GetBytes(value));
        }


        /// <summary>
        /// This is not the same as Add_Raw_Bytes(). This function will
        /// add the length, followed by the actual bytes.
        /// </summary>
        public void Add_Byte_Array(byte[] bytes)
        {
            this.bytes.AddRange(BitConverter.GetBytes(bytes.Length));
            this.bytes.AddRange(bytes);
        }


        /// <summary>
        /// This is not the same as Add_Byte_Array(). This function will
        /// not add the length of the input "bytes".
        /// </summary>
        public void Add_Raw_Bytes(IEnumerable<byte> bytes)
        {
            this.bytes.AddRange(bytes);
        }


        public void Add_2D_Bytes(byte[,] bytes_2d)
        {
            int height = bytes_2d.GetUpperBound(0) + 1;
            int width = bytes_2d.GetUpperBound(1) + 1;

            bytes.AddRange(BitConverter.GetBytes(height));
            bytes.AddRange(BitConverter.GetBytes(width));

            for (int row = 0; row < height; row++)
                for (int col = 0; col < width; col++)
                {
                    bytes.Add(bytes_2d[row, col]);
                }
        }


        public void Add_Int_List(List<int> int_list)
        {
            bytes.AddRange(BitConverter.GetBytes(int_list.Count));

            foreach (var value in int_list)
                bytes.AddRange(BitConverter.GetBytes(value));
        }


        public void Add_String(string str)
        {
            var str_bytes = Encoding.UTF8.GetBytes(str);

            bytes.AddRange(BitConverter.GetBytes(str_bytes.Length));
            bytes.AddRange(str_bytes);
        }


        public void Add_Int_HashSet(HashSet<int> set)
        {
            bytes.AddRange(BitConverter.GetBytes(set.Count));

            foreach (var value in set)
                bytes.AddRange(BitConverter.GetBytes(value));
        }


    }



    /// <summary>
    /// A class for decoding an array of bytes.
    /// </summary>
    class BytesReader
    {
        byte[] bytes;
        int index = 0; // advances as more bytes are read


        public BytesReader(byte[] bytes)
        {
            this.bytes = bytes;
        }


        public bool HasData
        {
            get
            {
                if (index < bytes.Length)
                    return true;
                else
                    return false;
            }
        }


        /// <summary>
        /// Reads a four byte integer, and increment the internal
        /// pointer by four.
        /// </summary>
        public int Read_Int()
        {
            int result = BitConverter.ToInt32(bytes, index);
            index += 4;
            return result;
        }


        /// <summary>
        /// Extracts a byte array. This is the mirror image of
        /// BytesWriter.Add_Byte_Array()
        /// </summary>
        public byte[] Read_Byte_Array()
        {
            int length = BitConverter.ToInt32(bytes, index);
            index += 4;

            byte[] result = new byte[length];
            Buffer.BlockCopy(bytes, index, result, 0, length);

            index += length;

            return result;
        }


        /// <summary>
        /// Increase the internal pointer by "length". In this use
        /// case, the external code will read from the "bytes"
        /// buffer directly.
        /// </summary>
        public void AdvancePointer(int length)
        {
            index += length;
        }

        /// <summary>
        /// Sets the internal "index" pointer to a particular offset.
        /// In normal use, the "index" is initialized to zero. The
        /// SetPointer() allows starting the read from the middle of
        /// the byte array.
        /// </summary>
        public void SetPointer(int offset)
        {
            index = offset;
        }

        /// <summary>
        /// Returns the current internal "index" location.
        /// </summary>
        public int GetPointerLocation()
        {
            return index;
        }


        /// <summary>
        /// Extracts a byte[,] object. This is the mirror image of
        /// BytesWriter.Add_2D_Bytes()
        /// </summary>
        public byte[,] Read_2D_Bytes()
        {
            int height = BitConverter.ToInt32(bytes, index);
            index += 4;

            int width = BitConverter.ToInt32(bytes, index);
            index += 4;

            var result = new byte[height, width];

            for (int row = 0; row < height; row++)
                for (int col = 0; col < width; col++)
                {
                    result[row, col] = bytes[index];
                    index++;
                }

            return result;
        }


        /// <summary>
        /// Extracts an int List object. This is the mirror image of
        /// BytesWriter.Add_Int_List()
        /// </summary>
        public List<int> Read_Int_List()
        {
            int count = BitConverter.ToInt32(bytes, index);
            index += 4;

            var result = new List<int>(count);

            for (int i = 0; i < count; i++)
            {
                int value = BitConverter.ToInt32(bytes, index);
                index += 4;

                result.Add(value);
            }

            return result;
        }


        /// <summary>
        /// Extract a string. This is the mirror image of 
        /// BytesWriter.Add_String()
        /// </summary>
        public string Read_String()
        {
            int length = BitConverter.ToInt32(bytes, index);
            index += 4;

            var result = Encoding.UTF8.GetString(bytes, index, length);
            index += length;

            return result;
        }


        /// <summary>
        /// Extracts a Int HashSet. This is the mirror image of
        /// BytesWriter.Add_Int_HashSet()
        /// </summary>
        public HashSet<int> Read_Int_HashSet()
        {
            int count = BitConverter.ToInt32(bytes, index);
            index += 4;

            var result = new HashSet<int>(count);

            for (int i = 0; i < count; i++)
            {
                int value = BitConverter.ToInt32(bytes, index);
                index += 4;

                result.Add(value);
            }

            return result;
        }
    }



}
