using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Charity.Models
{
    [MetadataType(typeof(CampaignMetadata))]
    public partial class Campaign
    {
        [NotMapped]
        public decimal TotalDonations
        {
            get
            {
                if (Donations != null && Donations.Any())
                {
                    return Donations.Where(d => d.Status == 1).Sum(d => d.Amount);
                }
                return 0;
            }
        }

        [NotMapped]
        public decimal ProgressPercentage
        {
            get
            {
                if (TargetAmount > 0)
                {
                    return Math.Min((TotalDonations / TargetAmount) * 100, 100);
                }
                return 0;
            }
        }
    }

    public class CampaignMetadata
    {
        [Required(ErrorMessage = "Vui lòng nhập tên chiến dịch.")]
        [StringLength(200, ErrorMessage = "Tên chiến dịch không quá 200 ký tự.")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Vui lòng đặt mục tiêu quyên góp.")]
        [Range(10000, double.MaxValue, ErrorMessage = "Số tiền mục tiêu tối thiểu là 10,000 VNĐ.")]
        public decimal TargetAmount { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu.")]
        public DateTime StartDate { get; set; }
    }
}