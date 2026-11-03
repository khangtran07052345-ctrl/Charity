using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using Charity.Models;
using Charity.Services;

namespace Charity.Controllers
{
    public class AdminController : Controller
    {
        private readonly CharityDBEntities database = new CharityDBEntities();

        private ActionResult RequireAdmin()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");
            if (Session["Role"] == null || Session["Role"].ToString() != "Admin")
                return RedirectToAction("Index", "Home");
            return null;
        }

        public ActionResult Index()
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            var campaigns = database.Campaigns.ToList();
            var validDonations = database.Donations.Where(d => d.Status == 1);
            ViewBag.TotalCampaigns = campaigns.Count;
            ViewBag.TotalUsers = database.Users.Count();
            ViewBag.TotalDonations = database.Donations.Count();
            ViewBag.ValidDonationCount = validDonations.Count();
            ViewBag.TotalAmount = validDonations.Select(d => (decimal?)d.Amount).Sum() ?? 0m;
            ViewBag.TotalTarget = campaigns.Select(c => (decimal?)c.TargetAmount).Sum() ?? 0m;
            ViewBag.Campaigns = new CampaignService(database).Search().OrderByDescending(c => c.EndDate).ToList();
            return View();
        }

        // Danh sách và lọc chiến dịch trong khu vực Admin.
        public ActionResult Campaigns(string keyword, string status)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;
            var list = new CampaignService(database).Search(keyword, status);
            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            return View(list);
        }

        [HttpGet]
        public ActionResult CreateCampaign()
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;
            LoadCategories();
            return View(new Campaign { StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(30), Status = 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateCampaign([Bind(Include = "CategoryId,Title,Description,TargetAmount,StartDate,EndDate,Status,ImageUrl")] Campaign campaign)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;
            ValidateCampaign(campaign);
            if (ModelState.IsValid)
            {
                campaign.CreatedAt = DateTime.Now;
                if (!campaign.Status.HasValue) campaign.Status = 0;
                database.Campaigns.Add(campaign);
                database.SaveChanges();
                TempData["Success"] = "Đã thêm chiến dịch thành công.";
                return RedirectToAction("Campaigns");
            }
            LoadCategories(campaign.CategoryId);
            return View(campaign);
        }

        [HttpGet]
        public ActionResult EditCampaign(int? id)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;
            if (!id.HasValue) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var campaign = database.Campaigns.Find(id.Value);
            if (campaign == null) return HttpNotFound();
            LoadCategories(campaign.CategoryId);
            return View(campaign);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditCampaign([Bind(Include = "Id,CategoryId,Title,Description,TargetAmount,StartDate,EndDate,Status,ImageUrl,CreatedAt")] Campaign input)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;
            ValidateCampaign(input);
            if (ModelState.IsValid)
            {
                var campaign = database.Campaigns.Find(input.Id);
                if (campaign == null) return HttpNotFound();
                campaign.CategoryId = input.CategoryId;
                campaign.Title = input.Title;
                campaign.Description = input.Description;
                campaign.TargetAmount = input.TargetAmount;
                campaign.StartDate = input.StartDate;
                campaign.EndDate = input.EndDate;
                campaign.Status = input.Status;
                campaign.ImageUrl = input.ImageUrl;
                database.SaveChanges();
                TempData["Success"] = "Đã cập nhật chiến dịch.";
                return RedirectToAction("Campaigns");
            }
            LoadCategories(input.CategoryId);
            return View(input);
        }

        [HttpGet]
        public ActionResult DeleteCampaign(int? id)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;
            if (!id.HasValue) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var campaign = database.Campaigns.Find(id.Value);
            if (campaign == null) return HttpNotFound();
            ViewBag.DonationCount = database.Donations.Count(d => d.CampaignId == campaign.Id);
            return View(campaign);
        }

        [HttpPost, ActionName("DeleteCampaign")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteCampaignConfirmed(int id)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;
            var campaign = database.Campaigns.Find(id);
            if (campaign == null) return HttpNotFound();
            if (database.Donations.Any(d => d.CampaignId == id))
            {
                TempData["Error"] = "Không thể xóa chiến dịch đã có khoản quyên góp. Bạn có thể chuyển trạng thái sang Đóng.";
                return RedirectToAction("Campaigns");
            }
            database.Campaigns.Remove(campaign);
            database.SaveChanges();
            TempData["Success"] = "Đã xóa chiến dịch.";
            return RedirectToAction("Campaigns");
        }

        public ActionResult Donations(string keyword, int? status)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;
            var query = database.Donations.Include(d => d.User).Include(d => d.Campaign).AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(d => d.TransactionCode.Contains(keyword) || d.User.FullName.Contains(keyword) || d.Campaign.Title.Contains(keyword));
            }
            if (status.HasValue) query = query.Where(d => d.Status == status.Value);
            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            ViewBag.ValidTotal = query.Where(d => d.Status == 1).Select(d => (decimal?)d.Amount).Sum() ?? 0m;
            return View(query.OrderByDescending(d => d.DonatedAt).ToList());
        }

        private void LoadCategories(int? selectedId = null)
        {
            ViewBag.CategoryId = new SelectList(database.Categories.OrderBy(c => c.Name).ToList(), "Id", "Name", selectedId);
        }

        private void ValidateCampaign(Campaign campaign)
        {
            if (campaign == null) return;

            if (string.IsNullOrWhiteSpace(campaign.Title))
                ModelState.AddModelError("Title", "Vui lòng nhập tên chiến dịch.");

            if (string.IsNullOrWhiteSpace(campaign.Description))
                ModelState.AddModelError("Description", "Vui lòng nhập mô tả chiến dịch.");

            if (campaign.TargetAmount <= 0)
                ModelState.AddModelError("TargetAmount", "Mục tiêu quyên góp phải lớn hơn 0.");

            if (campaign.EndDate <= campaign.StartDate)
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải sau ngày bắt đầu.");

            if (!campaign.Status.HasValue ||
                (campaign.Status.Value != 0 &&
                 campaign.Status.Value != 1 &&
                 campaign.Status.Value != 2))
            {
                ModelState.AddModelError("Status", "Trạng thái chiến dịch không hợp lệ.");
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) database.Dispose();
            base.Dispose(disposing);
        }
    }
}
