namespace ProgPOE.Controllers
{
    internal class DashboardViewModel
    {
        public string UserRole { get; set; }
        public string UserName { get; set; }
        public int TotalClaims { get; set; }
        public int PendingClaims { get; set; }
        public int ApprovedClaims { get; set; }
        public int RejectedClaims { get; set; }
        public decimal TotalEarnings { get; set; }
    }
}