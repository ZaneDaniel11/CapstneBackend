using System;

namespace Items.Models
{
    public class Item
    {
        public int ItemID { get; set; }            // Primary key for the item
        public int CategoryID { get; set; }        // Foreign key to category
        public string ItemName { get; set; }       // Name of the item
        public int Quantity { get; set; }          // Quantity of the item in stock
        public string Description { get; set; }    // Description of the item
        public DateTime DateAdded { get; set; }  
        public int CategoryViewID { get; set; }  // Date when the item was added to the inventory
    }
}
