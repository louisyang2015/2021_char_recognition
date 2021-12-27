using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharRecognitionLib
{
    public class BlackAndWhite_Image
    {
        byte[,] data;
        

        /// <summary>
        /// Construct an image by extracting the bytes starting at
        /// "data[offset]".
        /// </summary>
        public BlackAndWhite_Image(string type, int height, int width, 
            byte[] image_bytes, int offset)
        {
            // Allocate "data" and fill it in
            data = new byte[height, width];

            if (type == "G")
            {
                // Case: grayscale. Just copy from "image_bytes" to "data"
                for (int i = 0; i < height; i++)
                    for (int j = 0; j < width; j++)
                    {
                        data[i, j] = image_bytes[offset];
                        offset++;
                    }

                grayscale_to_black_white();
            }
            else if (type == "B")
            {
                // Case: black and white.
                BlackAndWhite_Image.Decode_from_bytes_to_BW_Image(image_bytes, offset, data);
            }
            else
                throw new Exception("Image error. Unknown image type.");
        }


        /// <summary>
        /// Decode a black and white image, stored at "image_bytes[offset]",
        /// into "data". The "data" must be already correctly allocated.
        /// The height and width information comes from "data".
        /// </summary>
        static public void Decode_from_bytes_to_BW_Image(byte[] image_bytes, 
            int offset, byte[,] data)
        {
            int height = data.GetUpperBound(0) + 1;
            int width = data.GetUpperBound(1) + 1;

            // Fill out "data" bit by bit.
            byte current_byte = image_bytes[offset];
            int bit_number = 0; // goes 0 to 7, then reset to 0

            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                {
                    // current_byte[MSB] --> data[i,j]
                    if ((current_byte & 0x80) != 0)
                        data[i, j] = 255;
                    else
                        data[i, j] = 0;

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
                            current_byte = image_bytes[offset];
                    }
                }
        }


        #region Image Processing

        /// <summary>
        /// Change "data" from grayscale to black and white.
        /// </summary>
        void grayscale_to_black_white()
        {
            // Find the intensity of the brightest pixel.
            byte brightest = 0;
            int height = data.GetUpperBound(0) + 1;
            int width = data.GetUpperBound(1) + 1;

            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                {
                    if (data[i, j] > brightest)
                        brightest = data[i, j];
                }


            // Establish three intensity ranges
            byte top_cutoff = (byte)((int)brightest * 2 / 3);
            byte bottom_cutoff = (byte)(brightest / 3);

            // catch case brightest = 1 or 2
            if (brightest == 1 || brightest == 2)
            {
                top_cutoff = brightest;
                bottom_cutoff = 0;
            }

            // Change top range to 255, and bottom range to 0
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                {
                    if (data[i, j] >= top_cutoff)
                        data[i, j] = 255;
                    else if (data[i, j] <= bottom_cutoff)
                        data[i, j] = 0;
                }

            // For the mid range pixels, set to 255 if it is 
            // bordering a 255 pixel.
            bool change;
            do
            {
                change = false;

                for (int i = 0; i < height; i++)
                    for (int j = 0; j < width; j++)
                    {
                        if (data[i, j] != 0 && data[i, j] != 255)
                        {
                            if (check_neighbors_for_value(i, j, 255))
                            {
                                data[i, j] = 255;
                                change = true;
                            }
                        }
                    }

            } while (change); // keep looping until no mid range pixel is
                              // bordering a 255 pixel

            // set all mid range pixels to 0
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                {
                    if (data[i, j] > 0 && data[i, j] < 255)
                        data[i, j] = 0;
                }
        }


        /// <summary>
        /// Check the neighbors of a pixel at (row, col) for "value".
        /// Returns true if one neighbor has this "value".
        /// </summary>
        bool check_neighbors_for_value(int row, int col, byte value)
        {
            int height = data.GetUpperBound(0) + 1;
            int width = data.GetUpperBound(1) + 1;

            // above
            if (row > 0)
            {
                if (col > 0)
                    if (data[row - 1, col - 1] == value) return true;

                if (data[row - 1, col] == value) return true;

                if (col < width - 1)
                    if (data[row - 1, col + 1] == value) return true;
            }

            // left
            if (col > 0)
                if (data[row, col - 1] == value) return true;

            // right
            if (col < width - 1)
                if (data[row, col + 1] == value) return true;

            // below
            if (row < height - 1)
            {
                if (col > 0)
                    if (data[row + 1, col - 1] == value) return true;

                if (data[row + 1, col] == value) return true;

                if (col < width - 1)
                    if (data[row + 1, col + 1] == value) return true;
            }

            return false;
        }

        /// <summary>
        /// Rescale "data" to a new size. This also re-centers the
        /// pixels.
        /// </summary>
        void rescale(int new_height, int new_width)
        {
            //////////////////////////////////////////////////////
            // Determines the parameters needed for rescaling

            // Find the current bounding box
            int old_height = data.GetUpperBound(0) + 1;
            int old_width = data.GetUpperBound(1) + 1;

            // Initial conditions for the bounding box:
            int left = old_width; // to be the smallest col value
            int top = old_height; // to be the smallest row value
            int right = 0; // to be the largest col value
            int bottom = 0; // to be the largest row value

            for (int row = 0; row < old_height; row++)
                for (int col = 0; col < old_width; col++)
                {
                    if (data[row, col] > 0)
                    {
                        if (left > col) left = col;
                        if (top > row) top = row;
                        if (right < col) right = col;
                        if (bottom < row) bottom = row;
                    }
                }

            // For now there is only down sample

            // Figure out the aspect ratio
            double ratio_hori = (double)(new_width - 1) / (right - left);
            double ratio_vert = (double)(new_height - 1) / (bottom - top);

            double ratio = ratio_hori;
            if (ratio > ratio_vert) ratio = ratio_vert;

            // If the bounding box is a tall rectangle, then there
            // will be offset on the left.
            int left_offset = 0;
            if ((right - left) < (bottom - top))
                // left_right_margin = new_image_width - resized_image_width
                // offset = left_right_margin / 2
                left_offset = (int)Math.Floor((new_width - (right - left) * ratio) / 2);

            if (left_offset < 0) left_offset = 0;

            // Similarily, if the bounding box is a wide rectangle, then
            // there will be offset on the top.
            int top_offset = 0;
            if ((right - left) > (bottom - top))
                top_offset = (int)Math.Floor((new_height - (bottom - top) * ratio) / 2);

            if (top_offset < 0) top_offset = 0;

            var rescale_param = (top, left, ratio, top_offset, left_offset,
                                new_height, new_width);


            //////////////////////////////////////////////////////
            // Allocate and fill in the new image
            var new_image = new byte[new_height, new_width];

            for (int row = 0; row < old_height; row++)
                for (int col = 0; col < old_width; col++)
                {
                    if (data[row, col] > 0)
                    {
                        // fill in the corresponding pixel in new_image
                        if (ratio <= 1)
                        {
                            // Downscaling
                            (int new_row, int new_col) = compute_rescaled_point(row, col, rescale_param);
                            new_image[new_row, new_col] = 255;
                        }
                        else
                        {
                            // upscaling is harder. Need to draw a line
                            // between adjacent pixels
                            upscale_point(row, col, new_image, rescale_param);
                        }
                    }
                }

            data = new_image;
        }


        /// <summary>
        /// Rescale a single point. Returns (new_row, new_col)
        /// </summary>
        (int, int) compute_rescaled_point(int row, int col, 
            (int top, int left, double ratio, int top_offset, int left_offset,
             int new_height, int new_width) rescale_param)
        {
            (int top, int left, double ratio, int top_offset, 
                int left_offset, int new_height, int new_width) = rescale_param;

            int new_row = top_offset + (int)Math.Round((row - top) * ratio);
            if (new_row > new_height - 1) new_row = new_height - 1;

            int new_col = left_offset + (int)Math.Round((col - left) * ratio);
            if (new_col > new_width - 1) new_col = new_width - 1;

            return (new_row, new_col);
        }


        /// <summary>
        /// Upscale a point from data[row, col] to new_image
        /// </summary>
        void upscale_point(int row, int col, byte[,] new_image,
            (int top, int left, double ratio, int top_offset, int left_offset,
             int new_height, int new_width) rescale_param)
        {
            (int new_row, int new_col) = compute_rescaled_point(row, col, rescale_param);
            new_image[new_row, new_col] = 255;

            int height = data.GetUpperBound(0) + 1;
            int width = data.GetUpperBound(1) + 1;

            // Look for 4/8 neighbors (above, upper right, right, lower right)
            // The other 4 neighbors will be handled when the other point,
            // such as the point below, is handled.

            // Above, upper right
            if (row > 1)
            {
                if (data[row - 1, col] > 0)
                {
                    // Draw line from current pixel to the one above
                    (int new_row2, int new_col2) = compute_rescaled_point(row - 1, col, rescale_param);
                    draw_line(new_row, new_col, new_row2, new_col2, new_image);
                }
                if (col < width - 1)
                {
                    if (data[row - 1, col + 1] > 0)
                    {
                        (int new_row2, int new_col2) = compute_rescaled_point(row - 1, col + 1, rescale_param);
                        draw_line(new_row, new_col, new_row2, new_col2, new_image);
                    }
                }
            }
            
            // Right
            if (col < width - 1)
            {
                if (data[row, col + 1] > 0)
                {
                    (int new_row2, int new_col2) = compute_rescaled_point(row, col + 1, rescale_param);
                    draw_line(new_row, new_col, new_row2, new_col2, new_image);
                }
            }

            // Lower right
            if (row < height - 1 && col < width - 1)
            {
                if (data[row + 1, col + 1] > 0)
                {
                    (int new_row2, int new_col2) = compute_rescaled_point(row + 1, col + 1, rescale_param);
                    draw_line(new_row, new_col, new_row2, new_col2, new_image);
                }
            }
        }

        
        /// <summary>
        /// Draw a line from (row1, col1) to (row2, col2)
        /// </summary>
        void draw_line(int row1, int col1, int row2, int col2, byte[,] image)
        {
            int height = Math.Abs(row1 - row2);
            int width = Math.Abs(col1 - col2);

            if (height > width)
            {
                // Proceed vertically
                // Make sure row1 is smaller than row2
                if (row1 > row2)
                {
                    (row1, row2) = (row2, row1);
                    (col1, col2) = (col2, col1);
                }

                double change = ((double)col2 - col1) / (row2 - row1);
                double col = col1 + change;

                for(int row = row1 + 1; row <= row2; row++)
                {
                    image[row, (int)Math.Round(col)] = 255;
                    col += change;
                }
            }
            else
            {
                // Proceed horizontally
                // Make sure col1 is smaller than col2
                if (col1 > col2)
                {
                    (row1, row2) = (row2, row1);
                    (col1, col2) = (col2, col1);
                }

                double change = ((double)row2 - row1) / (col2 - col1);
                double row = row1 + change;

                for (int col = col1 + 1; col <= col2; col++)
                {
                    image[(int)Math.Round(row), col] = 255;
                    row += change;
                }
            }
        }

        
        /// <summary>
        /// Thin the image, and then rescale its size to 8x8.
        /// </summary>
        public void Standardize()
        {
            K3M_Thinning.ThinImage(data);
            rescale(8, 8);
            
            // K3M_Thinning.ThinImage(data);
            // Sometimes it's better to thin again, but sometimes this
            // cause the image to lose detail or go off center.
            // So as of 2021-11-30, the decision for now is to 
            // thin only the original image, not the resized image.
        }

        #endregion


        /// <summary>
        /// Returns an array of bytes, one bit per pixel.
        /// </summary>
        public byte[] GetBytes()
        {
            // Allocate bytes
            int height = data.GetUpperBound(0) + 1;
            int width = data.GetUpperBound(1) + 1;
            var bytes = new byte[(int)Math.Ceiling(height * width / 8.0)];

            // Fill in "bytes"
            byte current_byte = 0;            
            int bit_counter = 0; // runs from 0 to 7
            byte bytes_index = 0; // runs from 0 to (bytes.Length - 1)

            for (int row = 0; row < height; row++)
                for (int col = 0; col < width; col++)
                {
                    current_byte = (byte)(current_byte << 1);

                    if (data[row, col] > 0)
                        current_byte++;

                    bit_counter++;
                    if (bit_counter >= 8)
                    {
                        bytes[bytes_index] = current_byte;

                        bit_counter = 0;
                        current_byte = 0;
                        bytes_index++;
                    }
                }

            return bytes;
        }


        /// <summary>
        /// Return byte[,], one byte per pixel.
        /// </summary>
        public byte[,] Get2DBytes() { return data; }
    }


    /// <summary>
    /// The K3M thinning algorithm, baesd on paper 
    /// "K3M: A universal algorithm for image skeletonization 
    /// and a review of thinning techniques" at:
    /// https://www.researchgate.net/publication/220273912
    /// </summary>
    class K3M_Thinning
    {
        // Image data
        static byte[,] data;
        static int width, height;


        /// <summary>
        /// Returns a byte that describes the neighborhood of 
        /// data[row, col], according to the K3M algorithm paper.
        /// </summary>
        static byte get_neighborhood_descriptor(int row, int col)
        {
            byte neighborhood = 0;

            // above
            if (row > 0)
            {
                if (col > 0)
                    if (data[row - 1, col - 1] > 0) neighborhood |= 0x80;

                if (data[row - 1, col] > 0) neighborhood |= 0x01;

                if (col < width - 1)
                    if (data[row - 1, col + 1] > 0) neighborhood |= 0x02;
            }

            // right
            if (col < width - 1)
                if (data[row, col + 1] > 0) neighborhood |= 0x04;

            // below
            if (row < height - 1)
            {
                if (col > 0)
                    if (data[row + 1, col - 1] > 0) neighborhood |= 0x20;

                if (data[row + 1, col] > 0) neighborhood |= 0x10;

                if (col < width - 1)
                    if (data[row + 1, col + 1] > 0) neighborhood |= 0x08;
            }

            // left
            if (col > 0)
                if (data[row, col - 1] > 0) neighborhood |= 0x40;

            return neighborhood;
        }


        //////////////////////////////////////////////////////////////
        // Lookup Table

        /// <summary>
        /// An 8 bit look up table
        /// </summary>
        class LookupTable
        {
            bool[] table = new bool[256];
            
            public bool LookUp(byte neighborhood)
            {
                return table[neighborhood];
            }


            /// <summary>
            /// The "indices" are where the table returns a "true".
            /// All other inputs will return a "false".
            /// </summary>
            public LookupTable(int[] indices)
            {
                foreach (var i in indices)
                    table[i] = true;
            }
        }

        static readonly LookupTable[] A;
        static readonly LookupTable A_1pix;


        //////////////////////////////////////////////////////////////
        // Setup

        static K3M_Thinning()
        {
            A = new LookupTable[6];

            // The look up tables come from the K3M paper
            A[0] = new LookupTable(new int[]
                { 3, 6, 7, 12, 14, 15, 24, 28, 30, 31, 48, 56, 60,
                62, 63, 96, 112, 120, 124, 126, 127, 129, 131, 135,
                143, 159, 191, 192, 193, 195, 199, 207, 223, 224,
                225, 227, 231, 239, 240, 241, 243, 247, 248, 249,
                251, 252, 253, 254 });

            A[1] = new LookupTable(new int[]
                { 7, 14, 28, 56, 112, 131, 193, 224 });

            A[2] = new LookupTable(new int[]
                { 7, 14, 15, 28, 30, 56, 60, 112, 120, 131, 135,
                  193, 195, 224, 225, 240 });

            A[3] = new LookupTable(new int[]
                { 7, 14, 15, 28, 30, 31, 56, 60, 62, 112, 120,
                124, 131, 135, 143, 193, 195, 199, 224, 225, 227,
                240, 241, 248 });

            A[4] = new LookupTable(new int[]
                { 7, 14, 15, 28, 30, 31, 56, 60, 62, 63, 112, 120,
                124, 126, 131, 135, 143, 159, 193, 195, 199, 207,
                224, 225, 227, 231, 240, 241, 243, 248, 249, 252 });

            A[5] = new LookupTable(new int[]
                { 7, 14, 15, 28, 30, 31, 56, 60, 62, 63, 112, 120,
                124, 126, 131, 135, 143, 159, 191, 193, 195, 199,
                207, 224, 225, 227, 231, 239, 240, 241, 243, 248,
                249, 251, 252, 254});

            A_1pix = new LookupTable(new int[]
                { 3, 6, 7, 12, 14, 15, 24, 28, 30, 31, 48, 56,
                60, 62, 63, 96, 112, 120, 124, 126, 127, 129, 131,
                135, 143, 159, 191, 192, 193, 195, 199, 207, 223,
                224, 225, 227, 231, 239, 240, 241, 243, 247, 248,
                249, 251, 252, 253, 254 });
        }


        //////////////////////////////////////////////////////////////
        // K3M algorithm steps
                
        record Coord(int row, int col);

        static LinkedList<Coord> border_pixels = new LinkedList<Coord>();

        static void phase_0()
        {
            border_pixels.Clear();

            for (int row = 0; row < height; row++)
                for (int col = 0; col < width; col++)
                {
                    if (data[row, col] > 0)
                    {
                        byte n = get_neighborhood_descriptor(row, col);
                        if (A[0].LookUp(n))
                            border_pixels.AddLast(new Coord(row, col));
                    }
                }
        }


        /// <summary>
        /// Phase 1 ~ 5. Returns a "modified" flag.
        /// </summary>
        static bool phase(int phase_number)
        {
            var border_pixel = border_pixels.First;
            bool modified = false;

            while (border_pixel != null)
            {
                // Check the "neighborhood" value for all border pixels
                int row = border_pixel.Value.row;
                int col = border_pixel.Value.col;
                byte n = get_neighborhood_descriptor(row, col);

                if (A[phase_number].LookUp(n))
                {
                    // If "neighborhood" value matches pre-established rules,
                    // fade the pixel to the background
                    data[row, col] = 0;

                    // Need to remove the "border_pixel" from the LinkedList.
                    // The removal is different if the border_pixel is the final
                    // pixel
                    border_pixel = border_pixel.Next;

                    if (border_pixel != null)
                        border_pixels.Remove(border_pixel.Previous);
                    else
                        border_pixels.RemoveLast();

                    modified = true;
                }
                else
                    border_pixel = border_pixel.Next;
            }

            return modified;
        }


        static void one_pixel_phase()
        {
            for (int row = 0; row < height; row++)
                for (int col = 0; col < width; col++)
                {
                    if (data[row, col] > 0)
                    {
                        byte n = get_neighborhood_descriptor(row, col);

                        if (A_1pix.LookUp(n))
                        {
                            // Fade pixel into the background
                            data[row, col] = 0;
                        }
                    }
                }
        }


        //////////////////////////////////////////////////////////////
        // K3M algorithm entry point

        static public void ThinImage(byte[,] data)
        {
            K3M_Thinning.data = data;
            height = data.GetUpperBound(0) + 1;
            width = data.GetUpperBound(1) + 1;

            // Phase 0 ~ 6
            bool modified;

            do
            {
                phase_0();

                modified = false;

                for (int i =1; i <= 5; i++)
                {
                    bool modified2 = phase(i);
                    if (modified2)
                        modified = true;
                }
            } while (modified);

            // 1-pixel width phase
            // one_pixel_phase();
            // As of 2021-11-30, this is deemed unnecessary. This will 
            // remove the stair case pattern, such as the letter 'X'.
            // Neighborhood value 12 and 24 will eliminate the downward
            // sloping stroke of the "X", starting from the upper left
            // corner.
        }
    }
}
