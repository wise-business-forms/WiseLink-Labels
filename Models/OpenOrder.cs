namespace WiseLabels.Models
{
    public class OpenOrder
    {
        public string? CustomerID { get; set; }
        public string? CustomerName { get; set; }
        public string? OrderedBy { get; set; }
        public string? CustomerOrderId { get; set; }
        public string? Site { get; set; }
        public DateTime? OrderDate { get; set; }
        public DateTime? OrderExpected { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public int? OrderedUnit { get; set; }
        public string? ProductDescription { get; set; }
        public decimal? ExpectedAmount { get; set; }
        public string? SHIPPED { get; set; }
        public string? PRODUCTION { get; set; }
        public string? INSTOCK { get; set; }
        public string? BonRef { get; set; }
        public List<string> Statuses { get; set; } = new();
    }
}
