using System;

namespace Charity.Models
{
    public class CampaignViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string ImageUrl { get; set; }

        public decimal TargetAmount { get; set; }

        // Tổng tiền quyên góp hợp lệ của chiến dịch
        public decimal RaisedAmount { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        // Quy ước trạng thái trong database:
        // 0 = Đang hoạt động
        // 1 = Đóng chiến dịch
        // 2 = Hoàn thành mục tiêu
        public int DbStatus { get; set; }

        // Tính phần trăm hoàn thành mục tiêu
        public int ProgressPercent
        {
            get
            {
                if (TargetAmount <= 0)
                    return 0;

                decimal percent = RaisedAmount / TargetAmount * 100m;

                if (percent >= 100m)
                    return 100;

                if (percent <= 0m)
                    return 0;

                return (int)Math.Round(percent);
            }
        }

        // Xác định trạng thái hiển thị
        public string StatusKey
        {
            get
            {
                // Ưu tiên kiểm tra đã đạt mục tiêu
                if (TargetAmount > 0 && RaisedAmount >= TargetAmount)
                    return "reached";

                // Chiến dịch đã được đóng thủ công
                if (DbStatus == 1)
                    return "ended";

                // Chiến dịch đã được đánh dấu hoàn thành
                if (DbStatus == 2)
                    return "reached";

                // Trạng thái không hợp lệ
                if (DbStatus != 0)
                    return "ended";

                // Chưa đến ngày bắt đầu
                if (StartDate.Date > DateTime.Today)
                    return "upcoming";

                // Đã qua ngày kết thúc
                if (EndDate.Date < DateTime.Today)
                    return "ended";

                // Đang trong thời gian hoạt động
                return "ongoing";
            }
        }

        // Nội dung trạng thái hiển thị
        public string StatusText
        {
            get
            {
                switch (StatusKey)
                {
                    case "reached":
                        return "Đạt mục tiêu";

                    case "ended":
                        return "Đã kết thúc";

                    case "upcoming":
                        return "Sắp diễn ra";

                    default:
                        return "Đang diễn ra";
                }
            }
        }

        // Màu nhãn trạng thái Bootstrap
        public string StatusBadgeClass
        {
            get
            {
                switch (StatusKey)
                {
                    case "reached":
                        return "bg-success";

                    case "ended":
                        return "bg-secondary";

                    case "upcoming":
                        return "bg-info";

                    default:
                        return "bg-primary";
                }
            }
        }
    }
}