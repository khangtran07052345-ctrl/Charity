using Charity.Models;
using System.Collections.Generic;
using System.Linq;

namespace Charity.Services
{
    public class CampaignService
    {
        private readonly CharityDBEntities db;

        public CampaignService(CharityDBEntities db)
        {
            this.db = db;
        }

        // Lấy danh sách chiến dịch, hỗ trợ tìm kiếm và lọc trạng thái
        public List<CampaignViewModel> Search(
            string keyword = null,
            string status = null)
        {
            var query = db.Campaigns.AsQueryable();

            // Tìm kiếm theo tên chiến dịch
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(c => c.Title.Contains(keyword));
            }

            // Lấy dữ liệu và tính tổng tiền quyên góp hợp lệ
            var list = query.Select(c => new CampaignViewModel
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                ImageUrl = c.ImageUrl,
                TargetAmount = c.TargetAmount,
                StartDate = c.StartDate,
                EndDate = c.EndDate,

                // 0 = đang hoạt động
                // 1 = hoàn thành hoặc không còn nhận quyên góp
                // 2 = đóng chiến dịch
                DbStatus = c.Status ?? 0,

                RaisedAmount = c.Donations
                    .Where(d => d.Status == 1)
                    .Sum(d => (decimal?)d.Amount) ?? 0m
            }).ToList();

            // Lọc theo trạng thái đã được tính trong CampaignViewModel
            if (!string.IsNullOrWhiteSpace(status))
            {
                list = list
                    .Where(c => c.StatusKey == status)
                    .ToList();
            }

            return list;
        }

        // Lấy chi tiết một chiến dịch
        public CampaignViewModel GetById(int id)
        {
            return Search()
                .FirstOrDefault(c => c.Id == id);
        }
    }
}