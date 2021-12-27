using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharRecognitionLib
{
    public class Template
    {
        // Format is value 0 for foreground, adjacent to foreground
        // pixels are 1, and all other pixels are 255
        byte[,] image;

        // Coordinates of the foreground pixels
        byte[] foreground_rows;
        byte[] foreground_cols;


        #region Constructors

        private Template() { }

        /// <summary>
        /// Fills internal "image" with "bytes", starting at "offset". 
        /// The "bytes" are encoded as one bit per pixel.
        /// </summary>
        public Template(byte[] bytes, int offset, int height, int width)
        {
            image = new byte[height, width];

            var rows_list = new List<byte>();
            var cols_list = new List<byte>();

            // Fill out "image" bit by bit.
            byte current_byte = bytes[offset];

            // Track current location in "bytes"
            int bit_number = 0; // goes 0 to 7, then reset to 0

            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                {
                    // current_byte[MSB] --> data[i,j]
                    if ((current_byte & 0x80) != 0)
                    {
                        // foreground bit
                        image[i, j] = 0;
                        rows_list.Add((byte)(i));
                        cols_list.Add((byte)(j));
                    }
                    else
                        // background bit
                        image[i, j] = 255;

                    bit_number++;
                    current_byte = (byte)(current_byte << 1);

                    if (bit_number >= 8)
                    {
                        bit_number = 0;
                        offset++;

                        // On the very final byte of the image, there will be no "next byte" to load
                        // So need to check "i" and "j" for the very end case
                        if ((i == height - 1) && (j == width - 1))
                        {
                            // do nothing - this is the final byte
                        }
                        else
                            current_byte = bytes[offset];
                    }
                }

            foreground_rows = rows_list.ToArray();
            foreground_cols = cols_list.ToArray();
        }


        /// <summary>
        /// Fills internal "image" with a black and white encoded image.
        /// This "bw_image" uses 0 for background, and non zero for 
        /// foreground.
        /// </summary>
        public Template(byte[,] bw_image)
        {
            // don't change bw_image, make a copy
            int height = bw_image.GetUpperBound(0) + 1;
            int width = bw_image.GetUpperBound(1) + 1;
            image = new byte[height, width];

            var rows_list = new List<byte>();
            var cols_list = new List<byte>();

            // Fill out "image" 
            for (int row = 0; row < height; row++)
                for (int col = 0; col < width; col++)
                {
                    if (bw_image[row, col] != 0)
                    {
                        // foreground pixel
                        image[row, col] = 0;
                        rows_list.Add((byte)(row));
                        cols_list.Add((byte)(col));
                    }
                    else
                        // background pixel
                        image[row, col] = 255;
                }

            foreground_rows = rows_list.ToArray();
            foreground_cols = cols_list.ToArray();
        }

        #endregion


        #region Set Neighbors

        /// <summary>
        /// Sets the neighboring pixels to ones. It's assumed that
        /// no change has ocurred to the image so far - that all
        /// pixels are still either 0 or 255.
        /// </summary>
        public void Set_4_neighbors_to_1()
        {
            for (int i = 0; i < foreground_rows.Length; i++)
            {
                int r = foreground_rows[i];
                int c = foreground_cols[i];

                // set neighbors to 1
                change_255_pixel(r - 1, c, 1);

                change_255_pixel(r, c - 1, 1);
                change_255_pixel(r, c + 1, 1);

                change_255_pixel(r + 1, c, 1);
            }
        }

        /// <summary>
        /// Sets the neighboring pixels to ones. It's assumed that
        /// no change has ocurred to the image so far - that all
        /// pixels are still either 0 or 255.
        /// </summary>
        void set_8_neighbors_to_1()
        {
            for (int i = 0; i < foreground_rows.Length; i++)
            {
                int r = foreground_rows[i];
                int c = foreground_cols[i];

                // set neighbors to 1
                change_255_pixel(r - 1, c - 1, 1);
                change_255_pixel(r - 1, c, 1);
                change_255_pixel(r - 1, c + 1, 1);

                change_255_pixel(r, c - 1, 1);
                change_255_pixel(r, c + 1, 1);

                change_255_pixel(r + 1, c - 1, 1);
                change_255_pixel(r + 1, c, 1);
                change_255_pixel(r + 1, c + 1, 1);
            }
        }

        /// <summary>
        /// Sets the neighboring pixels to 1's and 2's. It's 
        /// assumed that no change has ocurred to the image 
        /// so far - that all pixels are still either 0 or 255.
        /// </summary>
        void set_8_neighbors_to_1_2()
        {
            // Two passes
            // First pass, set immediate neighbors to 1

            for (int i = 0; i < foreground_rows.Length; i++)
            {
                int r = foreground_rows[i];
                int c = foreground_cols[i];

                // set immediate neighbors to 1
                change_255_pixel(r - 1, c, 1);

                change_255_pixel(r, c - 1, 1);
                change_255_pixel(r, c + 1, 1);

                change_255_pixel(r + 1, c, 1);
            }

            // Second pass, set corner neighbors to 2
            for (int i = 0; i < foreground_rows.Length; i++)
            {
                int r = foreground_rows[i];
                int c = foreground_cols[i];

                // set corner neighbors to 2
                change_255_pixel(r - 1, c - 1, 2);
                change_255_pixel(r - 1, c + 1, 2);

                change_255_pixel(r + 1, c - 1, 2);
                change_255_pixel(r + 1, c + 1, 2);
            }
        }


        /// <summary>
        /// if image[row, col] is 255, then change it to the provided
        /// "value". If a change is made, set the "change_flag" to true.
        /// </summary>
        void change_255_pixel(int row, int col, byte value)
        {
            if (row < 0) return;
            if (row > image.GetUpperBound(0)) return;
            if (col < 0) return;
            if (col > image.GetUpperBound(1)) return;
            if (image[row, col] != 255) return;

            image[row, col] = value;
        }

        #endregion


        /// <summary>
        /// For every background pixel in the current template,
        /// find the corresponding pixel value in "t2". Sum these
        /// pixel values. Perfect match will have a sum of zero.
        /// Non-matches will have a sum of 255.
        /// </summary>
        public int Diff(Template t2)
        {
            // Check pixel count first. 
            int pixel_diff = Foreground_Pixel_Count - t2.Foreground_Pixel_Count;
            if (pixel_diff < 0) pixel_diff = -1 * pixel_diff;

            // Allow for 25 % deviation between this template and t2
            // Early terminate due to large difference in pixel count
            int allowed_pixel_diff = Foreground_Pixel_Count / 4;
            if (pixel_diff > allowed_pixel_diff)
                return 255; 

            int diff = 0;

            for (int i = 0; i < foreground_rows.Length; i++)
            {
                int row = foreground_rows[i];
                int col = foreground_cols[i];

                int t2_val = t2.image[row, col];

                if (t2_val != 0 )
                {
                    if (t2_val == 255)
                        // Foreground pixel in this template is 
                        // background pixel in t2.
                        return 255; // early termination
                    else
                        diff += t2_val;
                }
            }

            return diff;
        }



        /////////////////////////////////////////////////////////////
        // External access to "image"

        public byte this[int row, int col]
        {
            get { return image[row, col]; }
        }

        public int Height { get { return image.GetUpperBound(0) + 1; } }
        public int Width { get { return image.GetUpperBound(1) + 1; } }

        public int Foreground_Pixel_Count
        {
            get { return foreground_rows.Length; }
        }

        #region Serialization

        public List<byte> ToBytes()
        {
            var writer = new BytesWriter();

            writer.Add_Byte_Array(foreground_rows);
            writer.Add_Byte_Array(foreground_cols);
            writer.Add_2D_Bytes(image);

            return writer.BytesList;
        }


        /// <summary>
        /// Rebuild a Template object from an array of bytes, starting at
        /// "offset".
        /// </summary>
        public static Template FromBytes(byte[] bytes, int offset)
        {
            var reader = new BytesReader(bytes);
            reader.SetPointer(offset);

            var template = new Template();

            template.foreground_rows = reader.Read_Byte_Array();
            template.foreground_cols = reader.Read_Byte_Array();
            template.image = reader.Read_2D_Bytes();

            return template;
        }

        #endregion


        #region Model Training

        class Pixel_RejectIDs : IComparable<Pixel_RejectIDs>
        {
            public int row, col; // location of the pixel
            public HashSet<int> reject_ids; // reject IDs at this pixel

            // The List.Sort() should prioritize objects with 
            // large amount of reject IDs.
            public int CompareTo(Pixel_RejectIDs pixel_reject_ids)
            {
                return pixel_reject_ids.reject_ids.Count - this.reject_ids.Count;
            }
        }


        /// <summary>
        /// Enable the neighborhood pixels in t1 and tune them to reject
        /// the templates in "reject_set". It's assumed that t1 still
        /// consist of only 0 and 255 pixels.
        /// </summary>
        public static void Train_to_reject(Template t1, List<Template> reject_set)
        {
            // Turn on t1's neighborhood pixels
            t1.Set_4_neighbors_to_1();

            /////////////////////////////////////////////////////////
            // Track which neighborhood pixel matches with which
            // template in the "reject_set"
            var reject_ids_2d = new HashSet<int>[t1.Height, t1.Width];

            var reject_ids = new HashSet<int>();

            for (int reject_id = 0; reject_id < reject_set.Count; reject_id++)
            {
                var r = reject_set[reject_id];
                int diff = r.Diff(t1);

                if (diff < 255)
                {
                    // Special case: perfect match between t1 and r
                    // Disabling the neighbors won't help. So nothing to
                    // do.
                    if (diff == 0)
                    {
                        // Maybe log this as a case of bad data
                    }
                    else
                    {
                        // Record "reject_id" for future. Some neighborhood
                        // pixel will have to be de-activated to reject 
                        // reject_set[reject_id]
                        reject_ids.Add(reject_id);

                        // Add "reject_id" to the relevant pixels.

                        // Go over the foreground pixels of "r"
                        for (int i = 0; i < r.foreground_rows.Length; i++)
                        {
                            int row = r.foreground_rows[i];
                            int col = r.foreground_cols[i];

                            // is t1[row, col] a neighborhood pixel?
                            if (t1[row, col] == 1)
                            {
                                // Make a note that t1[row, col] is used to
                                // match "reject_id"
                                if (reject_ids_2d[row, col] == null)
                                    reject_ids_2d[row, col] = new HashSet<int>();

                                reject_ids_2d[row, col].Add(reject_id);
                            }
                        }
                    }
                }
            }

            /////////////////////////////////////////////////////////
            // Compile the "reject_ids_2d" into a list
            var reject_ids_list = new List<Pixel_RejectIDs>();

            for (int row = 0; row < t1.Height; row++)
                for (int col = 0; col < t1.Width; col++)
                {
                    if (reject_ids_2d[row, col] != null)
                    {

                        reject_ids_list.Add(
                            new Pixel_RejectIDs() { 
                                row = row, col = col, 
                                reject_ids = reject_ids_2d[row, col] 
                            }
                            );
                    }
                }

            /////////////////////////////////////////////////////////
            // Deactivate neighborhood pixels to reject the reject_ids
            while (reject_ids.Count > 0)
            {
                reject_ids_list.Sort();

                // p means pixel_to_deactivate
                var p = reject_ids_list[0];

                if (p.reject_ids.Count == 0)
                    break; // exit while() loop
                    // No neighborhood pixel left to deactivate
                    // Make a note that this template is not useful
                    // for matching?

                t1.image[p.row, p.col] = 255;

                // clone p.reject_ids - these IDs need to 
                // be removed from both "reject_ids" and "reject_ids_list"
                var ids_taken_care_of = new List<int>(p.reject_ids.Count);

                foreach (var id in p.reject_ids)
                    ids_taken_care_of.Add(id);

                // Remove all of "ids_taken_care_of" from "reject_ids"
                foreach (var id in ids_taken_care_of)
                    reject_ids.Remove(id);

                foreach (var pixel in reject_ids_list)
                {
                    foreach (var id in ids_taken_care_of)
                        pixel.reject_ids.Remove(id);
                }
            }
        }

        #endregion
    }



    public class TemplateCollection
    {
        // All templates should be the same size
        public readonly int Height, Width;

        List<Template> templates = new List<Template>();

        // Parallel lists to track the label of the templates
        List<string> labels = new List<string>();
        List<int> indices = new List<int>();

        // An entry of labels = {"0", "1"} and indices = {0, 20}
        // would mean the "templates[0...19]" belong to the label "0",
        // and "templates[20..end]" belong to the label "1".


        public TemplateCollection(int height, int width) {
            Height = height;
            Width = width;
        }

        /// <summary>
        /// Add a set of templates to the internal "templates list".
        /// All of these templates share the same given "label".
        /// </summary>
        public void Add(Template[] templates, string label)
        {
            // Error checking
            foreach (var t in templates)
                check_height_and_width(t);

            labels.Add(label);
            indices.Add(this.templates.Count);
            this.templates.AddRange(templates);
        }


        /// <summary>
        /// Add a set of templates to the internal "templates list".
        /// All of these templates share the same given "label".
        /// </summary>
        public void Add(List<Template> templates, string label)
        {
            // Error checking
            foreach (var t in templates)
                check_height_and_width(t);

            labels.Add(label);
            indices.Add(this.templates.Count);
            this.templates.AddRange(templates);
        }


        /// <summary>
        /// Check that the height and width of the given "template"
        /// matches the height and width of this object. Throws an
        /// Exception on mismatch.
        /// </summary>
        void check_height_and_width(Template template)
        {
            if (template.Height != Height || template.Width != Width)
                throw new Exception("Encountered a corrupted template where "
                    + "the height and width does not match the collection's "
                    + "height and width.");
        }



        public Template GetTemplate(int template_id)
        {
            if (template_id < 0 || template_id > templates.Count - 1)
                throw new Exception("Attempted to retrieve a nonexistent template.");

            return templates[template_id];
        }


        /// <summary>
        /// Returns a label for a given "template_id".
        /// </summary>
        public string GetLabel(int template_id)
        {
            if (template_id < 0 || template_id > templates.Count - 1)
                throw new Exception("Attempted to retrieve label of a nonexistent template.");


            int index = indices.BinarySearch(template_id);

            if (index < 0)
            {
                // When "i" cannot be found, the "BinarySearch()"
                // returns the bitwise complement of the index
                // that contains next larger number.
                index = ~index;
                index--;
            }

            return labels[index];
        }


        /// <summary>
        /// Return templates that belong to a certain "label".
        /// </summary>
        List<Template> GetTemplates(string label)
        {
            var t_list = new List<Template>();

            for (int i = 0; i < labels.Count; i++)
            {
                if (labels[i] == label)
                {
                    // Determine the start and end indices
                    int start = indices[i];

                    int end = templates.Count - 1;
                    if (i < labels.Count - 1)
                        end = indices[i + 1] - 1;

                    // Add templates[start..end] to t_list
                    for (int j = start; j <= end; j++)
                        t_list.Add(templates[j]);
                }
            }

            return t_list;
        }


        /// <summary>
        /// Return all templates in the collection.
        /// </summary>
        public Template[] GetAllTemplates()
        {
            return templates.ToArray();
        }


        /// <summary>
        /// Add templates that contain "label" to the "label_templates" 
        /// list. Add templates that do not contain "label" to the
        /// "non_label_templates" list.
        /// </summary>
        public void Split_templates_into_two_groups(string label,
            List<Template> label_templates, List<Template> non_label_templates)
        {
            // Determine the range of "label" templates
            int start = -1;
            int end = -1;

            for (int i = 0; i < labels.Count; i++)
            {
                if (labels[i] == label)
                {
                    start = indices[i];

                    if (i < labels.Count - 1)
                        end = indices[i + 1] - 1;
                    else
                        end = templates.Count - 1;

                    break;
                }
            }

            // Special case: no template belong to "label"
            if (start == -1)
            {
                // "label" was not found in "labels"
                // Add all data to "non_label_templates"
                non_label_templates.AddRange(templates);
                return;
            }

            // Add templates before "label" to "non_label_templates"
            for (int i = 0; i < start; i++)
                non_label_templates.Add(templates[i]);

            // Add "label" templates to "label_templates"
            for (int i = start; i <= end; i++)
                label_templates.Add(templates[i]);

            // Add templates after "label" to "non_label_templates"
            for (int i = end + 1; i < templates.Count; i++)
                non_label_templates.Add(templates[i]);
        }

        /// <summary>
        /// Number of templates in this collection.
        /// </summary>
        public int Count
        {
            get { return templates.Count; }
        }


        /// <summary>
        /// Merge multiple TemplateCollection objects to produce a new one.
        /// </summary>
        public static TemplateCollection Merge(TemplateCollection[] collections)
        {
            // Error check
            if (collections.Length < 1)
                throw new Exception("Software attempted to merge nonexistent "
                    + "template collections.");

            var tc_new = new TemplateCollection(collections[0].Height,
                collections[0].Width);

            // Build up a set of labels
            var all_labels = new HashSet<string>();

            foreach (var tc in collections)
            {
                foreach (var label in tc.labels)
                    all_labels.Add(label);
            }

            // Build up the new template collection, one label at a time
            foreach (var label in all_labels)
            {
                // Collect all templates that belong to "label" in a list
                var t_list = new List<Template>();

                foreach (var tc in collections)
                {
                    var t_list2 = tc.GetTemplates(label);
                    t_list.AddRange(t_list2);
                }

                // At this point "t_list" contains all templates that belong
                // to "label".
                tc_new.Add(t_list, label);
            }

            return tc_new;
        }


        #region Serialization

        public List<byte> ToBytes()
        {
            var writer = new BytesWriter();

            // Add Height, Width
            writer.Add_Int(Height);
            writer.Add_Int(Width);

            // Add indices
            writer.Add_Int_List(indices);

            // Add labels
            writer.Add_Int(labels.Count);

            foreach (string label in labels)
                writer.Add_String(label);

            // Add templates
            writer.Add_Int(templates.Count);

            foreach (var t in templates)
            {
                var t_bytes = t.ToBytes();
                writer.Add_Int(t_bytes.Count);
                writer.Add_Raw_Bytes(t_bytes);
            }

            return writer.BytesList;
        }


        /// <summary>
        /// Build a template collection object from an array of bytes,
        /// starting at position "offset".
        /// </summary>
        public static TemplateCollection FromBytes(byte[] bytes, int offset = 0)
        {
            var reader = new BytesReader(bytes);
            reader.SetPointer(offset);

            int height = reader.Read_Int();
            int width = reader.Read_Int();

            var tc = new TemplateCollection(height, width);

            // Read in "indices"
            tc.indices = reader.Read_Int_List();

            // Read in "labels"
            int count = reader.Read_Int();
            tc.labels = new List<string>(count);

            for (int i = 0; i < count; i++)
                tc.labels.Add(reader.Read_String());

            // Read in "templates"
            count = reader.Read_Int();

            tc.templates = new List<Template>(count);

            for (int i = 0; i < count; i++)
            {
                int size = reader.Read_Int();

                var new_template = Template.FromBytes(bytes,
                    reader.GetPointerLocation());

                reader.AdvancePointer(size);

                tc.templates.Add(new_template);
            }

            return tc;
        }

        #endregion
    }



    class TemplateIndexEntry
    {
        public int row, col;
        public HashSet<int> template_numbers = new HashSet<int>();

        public static int compare_by_size(TemplateIndexEntry e1, TemplateIndexEntry e2)
        {
            return (e1.template_numbers.Count - e2.template_numbers.Count);
        }


        #region Serialization Functions

        public int[] To_int_array()
        {
            var i_array = new int[2 + template_numbers.Count];

            i_array[0] = row;
            i_array[1] = col;

            int i = 2;

            foreach (var number in template_numbers)
            {
                i_array[i] = number;
                i++;
            }

            return i_array;
        }


        /// <summary>
        /// Creates a TemplateIndexEntry object from the subset of an
        /// array.
        /// </summary>
        public static TemplateIndexEntry From_int_array(int[] i_array,
            int offset, int length)
        {
            if ((offset + length - 1 >= i_array.Length)
                || (length < 2))
                throw new Exception("Attempted to create an "
                    + "\"TemplateIndexEntry\" object with bad integer array.");

            var entry = new TemplateIndexEntry();
            entry.row = i_array[offset];
            entry.col = i_array[offset + 1];

            for (int i = offset + 2; i < offset + length; i++)
                entry.template_numbers.Add(i_array[i]);

            return entry;
        }

        #endregion
    }



    public class TemplateIndex
    {
        // All templates have the same width and height.
        int width, height;

        // pixel -> set {template numbers} look up tables
        TemplateIndexEntry[] index;


        /// <summary>
        /// Create pixel -> set {template numbers} look up tables.
        /// All templates have the same width and height.
        /// </summary>
        public TemplateIndex(Template[] templates, int first_id = 0)
        {
            height = templates[0].Height;
            width = templates[0].Width;

            ////////////////////////////////////////////////////
            // Build up the first version of the index, where 
            // index_2d[row, col] contains the template IDs
            var index_2d = new TemplateIndexEntry[height, width];

            for (int row = 0; row < height; row++)
                for (int col = 0; col < width; col++)
                {
                    index_2d[row, col] = new TemplateIndexEntry();
                    index_2d[row, col].row = row;
                    index_2d[row, col].col = col;
                }

            for (int i = 0; i < templates.Length; i++)
            {
                for (int row = 0; row < height; row++)
                    for (int col = 0; col < width; col++)
                    {
                        // On the i-th template's pixel [row, col]
                        if (templates[i][row, col] <= 1)
                            index_2d[row, col].template_numbers.Add(first_id + i);
                        // (first_id + i) is the ID for the i-th template
                    }
            }

            index = index_2d_to_1d(index_2d);

            // At this point, some entries in "index" might have
            // every single template present - as in coordinate (r, c)
            // matches with every single template.
            //
            // In the context of the current index, those entries are not
            // helpful in reducing the number of probabilities.
            // However, TemplateIndex objects are eventually merged together.
            // So the entry at (r, c) needs to be kept, because the same 
            // (r, c) in another TemplateIndex might not be completely full.
        }


        /// <summary>
        /// Convert index from the 2D "TemplateIndexEntry[,]" form 
        /// to 1D form.
        /// </summary>
        TemplateIndexEntry[] index_2d_to_1d(TemplateIndexEntry[,] index_2d)
        {
            ////////////////////////////////////////////////////
            // Create four lists, where each list holds data from
            // one of the four quadrants.
            var index_lists = new List<TemplateIndexEntry>[4];

            for (int i = 0; i < index_lists.Length; i++)
                index_lists[i] = new List<TemplateIndexEntry>();

            for (int row = 0; row < height; row++)
                for (int col = 0; col < width; col++)
                {
                    var current_index = index_2d[row, col];

                    if (current_index.template_numbers.Count > 0)
                    {
                        int quadrant_index = get_quadrant_index(row, col);
                        index_lists[quadrant_index].Add(index_2d[row, col]);
                    }
                }

            // Sort each list of index_lists based on the size of 
            // "TemplateIndexEntry.template_numbers"
            for (int i = 0; i < index_lists.Length; i++)
                index_lists[i].Sort(TemplateIndexEntry.compare_by_size);

            ////////////////////////////////////////////////////
            // Merge the four lists of index_lists[] into a single list
            var single_list = new List<TemplateIndexEntry>();

            bool new_item; // "True" means new item added to "single_list"

            int index = 0;

            do
            {
                new_item = false;

                for (int i = 0; i < index_lists.Length; i++)
                {
                    // i = 0, 1, 2, 3
                    // "index" will vary from 0 to end of each "index_lists"
                    if (index < index_lists[i].Count)
                    {
                        single_list.Add(index_lists[i][index]);
                        new_item = true;
                    }
                }

                index++;
            } while (new_item);

            return single_list.ToArray();
        }


        /// <summary>
        /// Maps a (row, col) coordinate into a quadrant number 
        /// between 0 and 3.
        /// </summary>
        int get_quadrant_index(int row, int col)
        {
            int quadrant_index;

            if (row <= height / 2 - 1)
                quadrant_index = 0;
            else
                quadrant_index = 2;

            if (col <= width / 2 - 1)
                quadrant_index += 0;
            else
                quadrant_index += 1;

            return quadrant_index;
        }


        /// <summary>
        /// Given an "image", return potential matches by searching
        /// through the "index".
        /// </summary>
        public int[] Search(Template image)
        {
            HashSet<int> possibilities = null;

            for (int i = 0; i < index.Length; i++)
            {
                int row = index[i].row;
                int col = index[i].col;

                if (image[row, col] == 0)
                {
                    // Match
                    if (possibilities == null)
                        possibilities = new HashSet<int>(index[i].template_numbers);
                    else
                    {
                        possibilities.IntersectWith(index[i].template_numbers);

                        // Early termination scenario
                        if (possibilities.Count == 0)
                            return new int[0];
                    }
                }
            }

            if (possibilities == null)
                return new int[0];
            else
                return possibilities.ToArray();
        }


        /// <summary>
        /// Convert index from the 1D "TemplateIndexEntry[]" form 
        /// to 2D form.
        /// </summary>
        TemplateIndexEntry[,] index_1d_to_2d(TemplateIndexEntry[] index_1d,
            int height, int width)
        {
            var index_2d = new TemplateIndexEntry[height, width];

            for (int i = 0; i < index_1d.Length; i++)
            {
                int row = index_1d[i].row;
                int col = index_1d[i].col;
                index_2d[row, col] = index_1d[i];
            }

            // Fill in the null holes
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                {
                    if (index_2d[i, j] == null)
                        index_2d[i, j] = new TemplateIndexEntry();
                }

            return index_2d;
        }


        /// <summary>
        /// Merge the content of another template index "i2" into this
        /// template index object.
        /// </summary>
        public void Merge(TemplateIndex i2)
        {
            // Error checks
            if ((height != i2.height) || (width != i2.width))
                throw new Exception("Software error. Attempted to merge two "
                    + "template index objects, but they differ in height or width.");

            // Expand the indices into 2D form.
            var index_2d = index_1d_to_2d(index, height, width);
            var i2_2d = index_1d_to_2d(i2.index, height, width);

            // Combine the index data in the 2D form
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                {
                    index_2d[i, j].template_numbers.UnionWith(i2_2d[i, j].template_numbers);
                }

            // Convert back into 1d form
            index = index_2d_to_1d(index_2d);
        }



        #region Serialization Functions

        public byte[] ToBytes()
        {
            // merge "index" into a single int array
            var i_list = new List<int>();

            i_list.Add(height);
            i_list.Add(width);

            foreach (var entry in index)
            {
                var i_array = entry.To_int_array();
                i_list.Add(i_array.Length);
                i_list.AddRange(i_array);
            }

            var bytes = new byte[i_list.Count * 4];
            var i_array2 = i_list.ToArray();

            Buffer.BlockCopy(i_array2, 0, bytes, 0, bytes.Length);

            return bytes;
        }


        public TemplateIndex(byte[] bytes)
        {
            // Error checking
            if (bytes.Length % 4 != 0)
                throw new Exception("Attempting to create a "
                    + "\"TemplateIndex\" object using corrupted bytes.");

            // Change "bytes" into "int" array
            var int_array = new int[bytes.Length / 4];

            Buffer.BlockCopy(bytes, 0, int_array, 0, bytes.Length);

            // Extract "height" and "width"
            height = int_array[0];
            width = int_array[1];

            // Build up a list of TemplateIndexEntry objects
            var list = new List<TemplateIndexEntry>();

            int i = 2;
            while (i < int_array.Length)
            {
                int block_size = int_array[i];
                i++;

                if (block_size == 0)
                    throw new Exception("Corrupted template index file.");

                var entry = TemplateIndexEntry.From_int_array(int_array, i, block_size);
                list.Add(entry);

                i += block_size;
            }

            index = list.ToArray();
        }

        #endregion
    }


}
