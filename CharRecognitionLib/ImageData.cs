using Azure.Storage.Blobs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CharRecognitionLib
{
    public class ImageData
    {



        #region index.tsv

        // index.tsv content
        // Label Type Height Width    Last File
        // 0      G     28     28        32
        // A      B     16     16        64

        // G = gray scale
        // B = black and white
        // Last File = 64 means last file is A_64.bin

        class IndexEntry
        {
            public string Label, Type;
            public int Height, Width, LastFile;

            public override string ToString()
            {
                return Label + '\t' + Type + '\t' + Height
                    + '\t' + Width + '\t' + LastFile;
            }

            public static IndexEntry TryParse(string line)
            {
                var tokens = line.Split('\t', options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (tokens.Length != 5) return null;

                try
                {
                    var index_entry = new IndexEntry();
                    index_entry.Label = tokens[0];
                    index_entry.Type = tokens[1];
                    index_entry.Height = int.Parse(tokens[2]);
                    index_entry.Width = int.Parse(tokens[3]);
                    index_entry.LastFile = int.Parse(tokens[4]);

                    // Limit image type to "B" and "G"
                    if (index_entry.Type != "B" && index_entry.Type != "G")
                        return null;

                    return index_entry;
                }
                catch
                {
                    return null;
                }
            }


            /// <summary>
            /// Returns bytes per image. Return -1 for unknown image type.
            /// </summary>
            public static int BytesPerImage(string type, int height, int width)
            {
                if (type == "B")
                    return (int)Math.Ceiling((double)(height * width / 8.0));

                else if (type == "G")
                    return height * width;

                else
                    return -1; // unknown image type
            }


            public int BytesPerImage()
            {
                return IndexEntry.BytesPerImage(Type, Height, Width);
            }
        }

        // Index to the data
        static Dictionary<string, IndexEntry> index;
        // The key is the label name



        static void write_index_to_blob_storage()
        {
            // Build "index" dictionary into a string
            var sb = new StringBuilder();

            // Header line
            sb.Append("Label\tType\tHeight\tWidth\tLast File\n");

            // One line per label
            foreach (var label in index.Keys)
            {
                var index_entry = index[label];
                sb.Append(index_entry.Label + '\t' + index_entry.Type + '\t'
                    + index_entry.Height + '\t' + index_entry.Width + '\t'
                    + index_entry.LastFile + '\n');
            }

            // Convert to binary format
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

            // Store to Azure
            Util.Upload_To_Storage("image-data", "index.tsv", bytes);
        }


        static void read_index_from_blob_storage()
        {
            index = new Dictionary<string, IndexEntry>();

            // Read from Azure
            var bytes = Util.Download_From_Storage("image-data", "index.tsv");

            if (bytes == null) return;

            // Bytes to string
            string index_str = Encoding.UTF8.GetString(bytes);

            // String to dictionary<string, int>
            var lines = index_str.Split("\n");

            foreach (var line in lines)
            {
                var entry = IndexEntry.TryParse(line);
                if (entry is not null)
                    index[entry.Label] = entry;
            }
        }

        #endregion



        static ImageData()
        {
            read_index_from_blob_storage();
        }


        #region Storage conventions

        // The images per file is locked at 32. 
        // Example:
        // image-data/A_0.bin (contains images 0 ~ 31 for label "A")
        // image-data/A_32.bin (contains images 32 ~ 63 for label "A")
        const int max_images_per_file = 32;

        public static int MaxImagesPerFile
        {
            get { return max_images_per_file; }
        }

        public const string Original_Image_Location = "original/";
        public const string Standard_Image_Location = "standard/";

        #endregion


        /// <summary>
        /// Returns all labels in the index.
        /// </summary>
        public static string[] Get_All_Labels()
        {
            return index.Keys.ToArray();
        }


        /// <summary>
        /// Return the bytes belonging to the data at "label_number.bin".
        /// If the ".bin" file does not exist, return "null".
        /// </summary>
        public static byte[] Get_Label_Bytes(string prefix, string label, int data_number)
        {
            data_number = data_number / max_images_per_file;
            data_number = data_number * max_images_per_file;

            string blob_name = prefix + label + '_' + data_number + ".bin";

            return Util.Download_From_Storage("image-data", blob_name);
        }



        /// <summary>
        /// Return the bytes belonging to the images described by "prefix",
        /// "label", and "sorted_image_numbers". It's assumed that the
        /// image numbers have been sorted already.
        /// If there is no data, or there's an error, return "null".
        /// </summary>
        public static byte[] Get_Label_Bytes_By_Numbers(string prefix, 
            string label, int[] sorted_image_numbers)
        {
            // Error checking
            if (index.ContainsKey(label) == false)
                return null;

            if (sorted_image_numbers == null) return null;


            List<byte> image_bytes = new List<byte>();

            // The images can be over multiple files
            int current_file_number = -1;
            byte[] current_file_bytes = null;

            // Stats for this set of images
            var entry = index[label];
            int bytes_per_image = entry.BytesPerImage();
            int max_file_number = entry.LastFile;

            // Standardized image is always 8 bytes
            if (prefix == ImageData.Standard_Image_Location)
                bytes_per_image = 8;

            foreach (var n in sorted_image_numbers)
            {
                int file_number = n / max_images_per_file * max_images_per_file;
                int offset_number = n - file_number;

                // Error checks
                if (file_number > max_file_number) return null;
                if (offset_number > max_images_per_file) return null;

                // Case: load a new file -> "current_file_bytes"
                if (file_number != current_file_number)
                {
                    string blob_name = prefix + label + '_' + file_number + ".bin";
                    current_file_bytes = Util.Download_From_Storage("image-data", blob_name);
                    current_file_number = file_number;
                }

                // Add image from "current_file_bytes" to "image_bytes"
                int start_index = offset_number * bytes_per_image;
                int end_index = (offset_number + 1) * bytes_per_image;
                image_bytes.AddRange(current_file_bytes[start_index..end_index]);
            }

            return image_bytes.ToArray();
        }


        /// <summary>
        /// For a given "label", return the ("type", "height", "width", 
        /// "last_file_number").
        /// </summary>
        public static (string, int, int, int) Get_Label_Stats(string label)
        {
            if (index.ContainsKey(label))
            {
                var entry = index[label];
                return (entry.Type, entry.Height, entry.Width, entry.LastFile);
            }
            else
                return (null, 0, 0, 0);
        }


        /// <summary>
        /// Add a single image to Azure storage. Returns a success flag.
        /// </summary>
        public static bool Add_Image(string label, byte[] image_bytes,
            string type, int height, int width)
        {
            // All images added to this function will be placed under
            // "original/"
            string prefix = Original_Image_Location;

            // Case: new label
            if (index.ContainsKey(label) == false)
            {
                // New label previously not in "index"
                var entry = new IndexEntry();
                entry.Label = label;
                entry.Type = type;
                entry.Height = height;
                entry.Width = width;
                entry.LastFile = 0;

                string blob_name = prefix + label + "_0.bin";

                Util.Upload_To_Storage("image-data", blob_name, image_bytes);

                Standardize_One_Image(image_bytes, entry);

                index[label] = entry;
                write_index_to_blob_storage();

                return true;
            }
            else
            {
                // Case: Existing label
                var entry = index[label];

                // Check "type", "height", and "width" parameters
                if (type != entry.Type) return false;
                if (height != entry.Height) return false;
                if (width != entry.Width) return false;

                // Figure out the maximum length of a blob
                int bytes_per_image = entry.BytesPerImage();

                if (bytes_per_image < 0)
                    return false; // unknown image type

                // Read existing bytes
                var bytes = Get_Label_Bytes(prefix, label, entry.LastFile);

                if (bytes.Length >= bytes_per_image * max_images_per_file)
                {
                    // Case: The new image needs to go into a new file
                    entry.LastFile += max_images_per_file;

                    string blob_name = prefix + label + '_' + entry.LastFile + ".bin";

                    Util.Upload_To_Storage("image-data", blob_name, image_bytes);

                    Standardize_One_Image(image_bytes, entry);

                    write_index_to_blob_storage();

                    return true;
                }
                else
                {
                    // Case: The new image can be appended to existing bytes
                    var new_bytes = new byte[bytes.Length + image_bytes.Length];
                    Array.Copy(bytes, new_bytes, bytes.Length);
                    Array.Copy(image_bytes, 0, new_bytes, bytes.Length, image_bytes.Length);

                    string blob_name = prefix + label + '_' + entry.LastFile + ".bin";

                    Util.Upload_To_Storage("image-data", blob_name, new_bytes);

                    Standardize_One_Image(image_bytes, entry);

                    return true;
                }
            }
        }


        /// <summary>
        /// Add multiple images to Azure storage. Returns a success flag.
        /// The "image_bytes" can be large, spanning multiple files.
        /// </summary>
        public static bool Add_Images(string label, byte[] image_bytes,
            string type, int height, int width)
        {
            // All images added to this function will be placed under
            // "original/"
            string prefix = Original_Image_Location;

            ////////////////////////////////////////////////////////////
            // Error checking

            IndexEntry entry = null; // This will be "index[label]"

            // If the index already contain this label, make sure the 
            // type, height, and width match.
            if (index.ContainsKey(label))
            {
                entry = index[label];
                if (type != entry.Type) return false;
                if (height != entry.Height) return false;
                if (width != entry.Width) return false;
            }


            ////////////////////////////////////////////////////////////
            // Variables

            // Figure out the size of an image
            int bytes_per_image = IndexEntry.BytesPerImage(type, height, width);

            if (bytes_per_image < 0)
                return false; // unknown image type
                        
            byte[] bytes = null;     // buffer

            int offset = 0;     // current location in the "image_bytes" buffer


            ////////////////////////////////////////////////////////////
            // To reduce potential corruption, first "fill up" the current file.

            if (index.ContainsKey(label))
            {
                entry = index[label];
                bytes = Get_Label_Bytes(prefix, label, entry.LastFile);

                if (bytes.Length < bytes_per_image * max_images_per_file)
                {
                    // Case: Some data can be appended to existing bytes
                    int free_space = bytes_per_image * max_images_per_file - bytes.Length;

                    // To reduce chance for corruption, only put in full images in the
                    // current file, no partial images.

                    // Build "new_bytes" containing old data, plus part of the new
                    // "image_bytes" data
                    int length_to_copy = free_space - (free_space % bytes_per_image);

                    var new_bytes = new byte[bytes.Length + length_to_copy];
                    Array.Copy(bytes, new_bytes, bytes.Length);
                    Array.Copy(image_bytes, 0, new_bytes, bytes.Length, length_to_copy);

                    offset = length_to_copy;

                    string blob_name = prefix + label + '_' + entry.LastFile + ".bin";

                    Util.Upload_To_Storage("image-data", blob_name, new_bytes);
                }
            }


            // From this point onward, there will only be creation of
            // new "label_number.bin" files. The "index" is updated only
            // at the very end.

            ////////////////////////////////////////////////////////////
            // Determine file number

            int file_number = 0;
            // Case: New label previously not in "index"
            // The file number will start at 0.

            if (index.ContainsKey(label))
            {
                // Case: Label already in "index".
                // The "label_number.bin" file has been previously filled up,
                // so the file number is after "LastFile".
                file_number = index[label].LastFile + max_images_per_file;
            }

            while (offset < image_bytes.Length)
            {
                // Write "image_bytes[offset...]" to "label_file_number.bin"
                string blob_name = prefix + label + '_' + file_number + ".bin";

                 if (image_bytes.Length > (offset + max_images_per_file * bytes_per_image - 1))
                {
                    // There is enough data for a full "label_number.bin" file.
                     // Upload (max_images_per_file * bytes_per_image) bytes.

                    Util.Upload_To_Storage("image-data", blob_name,
                        image_bytes[offset..(offset + max_images_per_file * bytes_per_image)]);

                     offset += max_images_per_file * bytes_per_image;
                    file_number += max_images_per_file;
                }
                else
                {
                    // Upload just the remainder of "image_bytes"
                    Util.Upload_To_Storage("image-data", blob_name,
                        image_bytes[offset..(image_bytes.Length)]);

                    break;
                }
            }

            ////////////////////////////////////////////////////////////
            // Index update needed

            // Index update is last to minimize corruption

            if (index.ContainsKey(label) == false)
            {
                // Case: New label previously not in "index"
                // Create index entry, and 
                entry = new IndexEntry();
                entry.Label = label;
                entry.Type = type;
                entry.Height = height;
                entry.Width = width;
                entry.LastFile = file_number;

                index[label] = entry;
            }
            else
            {
                index[label].LastFile = file_number;
            }
                        
            write_index_to_blob_storage();

            return true;
        }


        /// <summary>
        /// Generate standardized images for a particular ".bin" file.
        /// </summary>
        public static void StandardizeImages(string label, int image_number)
        {
            /////////////////////////////////////////////////////////
            // Error checking
            if (index.ContainsKey(label) == false)
                throw new Exception("Attempt to standardize non-existing image set.");

            // Round down "image_number" to "file_number"
            int file_number = (image_number / max_images_per_file) * max_images_per_file;

            // Read bytes
            var bytes = Get_Label_Bytes(Original_Image_Location, label, file_number);
            if (bytes == null)
                throw new Exception("Attempt to standardize nonexistent file.");


            /////////////////////////////////////////////////////////
            // Image standardization
            var entry = index[label];
            int bytes_per_image = entry.BytesPerImage();

            int offset = 0; // will point to the start of image inside "bytes"

            var new_bytes = new List<byte>(); // bytes for the standardized image

            while (offset < bytes.Length)
            {
                var image = new BlackAndWhite_Image(entry.Type, entry.Height,
                    entry.Width, bytes, offset);

                image.Standardize();
                new_bytes.AddRange(image.GetBytes());

                offset += bytes_per_image;
            }

            /////////////////////////////////////////////////////////
            // Write to Azure Storage
            var blob_name = Standard_Image_Location + label + '_' + file_number + ".bin";

            Util.Upload_To_Storage("image-data", blob_name, new_bytes.ToArray());
        }



        /// <summary>
        /// Standardize one image and upload it to Azure. This function does
        /// not modify the index. 
        /// </summary>
        static void Standardize_One_Image( byte[] image_bytes,
            IndexEntry entry)
        {
            // It's assumed that adding the original image already
            // modified the index. The Add_Image(...) will update the index,
            // then call this function, to add a standardized image to
            // the "standard/" directory.
        
            /////////////////////////////////////////////////////////
            // Image standardization
            var image = new BlackAndWhite_Image(entry.Type, entry.Height,
                    entry.Width, image_bytes, offset: 0);

            image.Standardize();

            var new_image_bytes = image.GetBytes();
            
            /////////////////////////////////////////////////////////
            // Write image to Azure Storage

            // Read bytes for previous standardized images

            var old_images_bytes = Get_Label_Bytes(Standard_Image_Location, entry.Label, entry.LastFile);
            byte[] all_bytes = null;

            if (old_images_bytes == null)
            {
                // this is a new file, with the current image being the newest image
                all_bytes = new_image_bytes;
            }
            else
            {
                all_bytes = new byte[old_images_bytes.Length + new_image_bytes.Length];
                Array.Copy(old_images_bytes, all_bytes, old_images_bytes.Length);
                Array.Copy(new_image_bytes, 0, all_bytes, old_images_bytes.Length, new_image_bytes.Length);
            }

            var blob_name = Standard_Image_Location + entry.Label + '_' + entry.LastFile + ".bin";

            Util.Upload_To_Storage("image-data", blob_name, all_bytes);
        }

    }



    /// <summary>
    /// Standardize all images by using a serverless endpoint.
    /// </summary>
    public class Standardize_All_Images_Task
    {
        string[] all_labels = ImageData.Get_All_Labels();

        int current_label_index = 0; // current label that we are working on
        int latest_file_number = 0; // the next file number to work on

        public readonly static object lock_object = new object();


        /// <summary>
        /// Returns a (label, file_numbers), which represents the 
        /// workload to be done.
        /// </summary>
        (string, string) get_work()
        {
            // First call returns ("0", "0,32,64,...")
            // Next call returns ("0", "320,352,...")
            // and so on...

            lock (Standardize_All_Images_Task.lock_object)
            {
                // Case: nothing more to work on
                if (current_label_index >= all_labels.Length) return (null, null);

                // Build up the "file_numbers" string
                var file_numbers = new StringBuilder();

                string label = all_labels[current_label_index];
                (_, _, _, int last_file_number) = ImageData.Get_Label_Stats(label);

                int start = latest_file_number;
                int delta = ImageData.MaxImagesPerFile;
                int end = latest_file_number + 10 * delta;

                if (end > last_file_number) end = last_file_number;

                // The "file_numbers" is comma separated, it looks like:
                // "0,32,64,96"
                for (int i = start; i <= end - delta; i += delta)
                    file_numbers.Append(i + ",");

                file_numbers.Append(end);

                // Increment the state variables for the next call to this function
                latest_file_number = end + delta;

                if (latest_file_number > last_file_number)
                {
                    current_label_index++;
                    latest_file_number = 0;
                }

                return (label, file_numbers.ToString());
            }
        }


        record API_Input(string label, string file_numbers);

        void do_work()
        {
            var client = new HttpClient();
            var random = new Random();

            while (true)
            {
                // Figure out the next task
                (var label, var file_numbers) = get_work();

                if (label == null) break;

                ///////////////////////////////////////////////////////
                // Call serverless function to accomplish task
                var api_input = new API_Input(label, file_numbers);
                var json = JsonConvert.SerializeObject(api_input);

                // endpoint URL comes from "Credentials.cs"
                string url = Credentials.Azure_Standardize_Image;

                // Azure functions often return error 500
                // So retry is needed
                string result = "";
                int retry = 0;
                do
                {
                    var response = client.PostAsync(url,
                    new StringContent(json, Encoding.UTF8, "application/json"));

                    response.Wait();

                    result = response.Result.Content.ReadAsStringAsync().Result;

                    if (result != "OK")
                    {
                        Thread.Sleep(random.Next(100, 300));
                        retry++;
                    }
                } while (result != "OK");



                // The "file_numbers" output is too long to be printed
                // Format a shorter output
                int first_comma = file_numbers.IndexOf(',');
                int last_comma = file_numbers.LastIndexOf(',');

                string file_numbers_short = file_numbers;

                if (first_comma > 0 && last_comma > 0 && last_comma > first_comma)
                {
                    file_numbers_short = file_numbers.Substring(0, file_numbers.IndexOf(',') + 1)
                        + " ... " + file_numbers.Substring(file_numbers.LastIndexOf(','));
                }
                                
                lock (Console.Out)
                {
                    Console.WriteLine("Label: " + label + " files " 
                        + file_numbers_short + " " + result + " after "
                        + retry + " retries");
                }
            }
        }


        public void Start()
        {
            // Create threads
            var threads = new Thread[20];

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(do_work);
                threads[i].Start();

                // Separate thread starting times so to make the debug
                // experience more consistent.
                Thread.Sleep(50);
            }

            // Wait for threads to end
            for (int i = 0; i < threads.Length; i++)
                threads[i].Join();

            Console.WriteLine("All images have been standardized.");
        }
    }
}
