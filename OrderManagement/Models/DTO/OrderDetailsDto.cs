﻿using OrderManagement.Common;

namespace OrderManagement.Models.DTO
{
    public class OrderDetailsDto
    {
        public Guid OrderDetailID { get; set; }
        public Guid ProductID { get; set; }
        public string ProductName { get; set; } //Include Product Name
        public Guid ManufacturerID { get; set; }
        public string ManufacturerName { get; set; } //Include Manufacturer Name
        public int Quantity { get; set; }
        public int OrderItemStatus { get; set; }
        public string OrderItemStatusValue => Enum.GetName(typeof(OrderStatus), OrderItemStatus); // ✅ Get Enum Name
        public decimal ProductPrice { get; set; }
    }
}
