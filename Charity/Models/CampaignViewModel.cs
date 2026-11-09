
using System;

namespace Charity.Models
{
    public class CampaignViewModel
    {
        // ID chiến dịch
        public int Id { get; set; }

        // Thông tin chiến dịch
        public string Title { get; set; }

        public string Description { get; set; }

        // Đường dẫn ảnh được Admin tải lên
        public string ImageUrl { get; set; }

        // Số tiền mục tiêu
        public decimal TargetAmount { get; set; }

        // Tổng tiền quyên góp hợp lệ
        public decimal RaisedAmount { get; set; }

        // Thời gian chiến dịch
        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        // Trạng thái lưu trong database:
        // 0 = Đang hoạt động
        // 1 = Đã đóng
        // 2 = Hoàn thành mục tiêu
        public int DbStatus { get; set; }

        // Phần trăm hoàn thành mục tiêu
        public int ProgressPercent
        {
            get
            {
                if (TargetAmount <= 0 || RaisedAmount <= 0)
                    return 0;

                decimal percent = RaisedAmount / TargetAmount * 100m;

                if (percent >= 100m)
                    return 100;

                return (int)Math.Round(percent);
            }
        }

        // Khóa trạng thái chiến dịch
        public string StatusKey
        {
            get
            {
                // Đã đạt mục tiêu
                if (TargetAmount > 0 && RaisedAmount >= TargetAmount)
                    return "reached";

                // Đã đóng thủ công
                if (DbStatus == 1)
                    return "ended";

                // Đã được đánh dấu hoàn thành
                if (DbStatus == 2)
                    return "reached";

                // Trạng thái không hợp lệ
                if (DbStatus != 0)
                    return "ended";

                // Chưa đến ngày bắt đầu
                if (StartDate.Date > DateTime.Today)
                    return "upcoming";

                // Đã hết hạn
                if (EndDate.Date < DateTime.Today)
                    return "ended";

                // Đang hoạt động
                return "ongoing";
            }
        }

        // Tên trạng thái hiển thị
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
