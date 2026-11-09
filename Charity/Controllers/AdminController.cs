
using Charity.Models;
using Charity.Services;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace Charity.Controllers
{
    public class AdminController : Controller
    {
        private readonly CharityDBEntities database = new CharityDBEntities();

        // Kiểm tra quyền Admin
        private ActionResult RequireAdmin()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

            if (Session["Role"] == null ||
                Session["Role"].ToString() != "Admin")
                return RedirectToAction("Index", "Home");

            return null;
        }

        // =========================
        // DASHBOARD
        // =========================
        public ActionResult Index()
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            var campaigns = database.Campaigns.ToList();

            var validDonations =
                database.Donations.Where(d => d.Status == 1);

            ViewBag.TotalCampaigns = campaigns.Count;
            ViewBag.TotalUsers = database.Users.Count();
            ViewBag.TotalDonations = database.Donations.Count();

            ViewBag.ValidDonationCount = validDonations.Count();

            ViewBag.TotalAmount =
                validDonations.Select(d => (decimal?)d.Amount).Sum() ?? 0m;

            ViewBag.InvalidDonationCount =
                database.Donations.Count(d => d.Status != 1);

            ViewBag.TotalTarget =
                campaigns.Select(c => (decimal?)c.TargetAmount).Sum() ?? 0m;

            ViewBag.Campaigns = new CampaignService(database)
                .Search()
                .OrderByDescending(c => c.EndDate)
                .ToList();

            return View();
        }

        // =========================
        // DANH SÁCH CHIẾN DỊCH
        // =========================
        public ActionResult Campaigns(string keyword, string status)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            var list = new CampaignService(database)
                .Search(keyword, status);

            ViewBag.Keyword = keyword;
            ViewBag.Status = status;

            return View(list);
        }

        // =========================
        // TẠO CHIẾN DỊCH - GET
        // =========================
        [HttpGet]
        public ActionResult CreateCampaign()
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            LoadCategories();

            return View(new Campaign
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(30),
                Status = 0
            });
        }

        // =========================
        // TẠO CHIẾN DỊCH - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateCampaign(
            [Bind(Include = "CategoryId,Title,Description,TargetAmount,StartDate,EndDate,Status")]
            Campaign input,
            HttpPostedFileBase campaignImage)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            ValidateCampaign(input);
            ValidateCampaignImage(campaignImage);

            if (ModelState.IsValid)
            {
                var campaign = new Campaign();

                campaign.CategoryId = input.CategoryId;
                campaign.Title = input.Title;
                campaign.Description = input.Description;
                campaign.TargetAmount = input.TargetAmount;
                campaign.StartDate = input.StartDate;
                campaign.EndDate = input.EndDate;
                campaign.Status = input.Status;

                campaign.CreatedAt = DateTime.Now;

                // Lưu ảnh nếu Admin đã chọn file
                if (campaignImage != null &&
                    campaignImage.ContentLength > 0)
                {
                    campaign.ImageUrl = SaveCampaignImage(campaignImage);
                }

                database.Campaigns.Add(campaign);
                database.SaveChanges();

                TempData["Success"] = "Đã tạo chiến dịch thành công.";

                return RedirectToAction("Campaigns");
            }

            LoadCategories(input.CategoryId);
            return View(input);
        }

        // =========================
        // SỬA CHIẾN DỊCH - GET
        // =========================
        [HttpGet]
        public ActionResult EditCampaign(int? id)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            if (!id.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var campaign = database.Campaigns.Find(id.Value);

            if (campaign == null)
                return HttpNotFound();

            LoadCategories(campaign.CategoryId);

            return View(campaign);
        }

        // =========================
        // SỬA CHIẾN DỊCH - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditCampaign(
            [Bind(Include = "Id,CategoryId,Title,Description,TargetAmount,StartDate,EndDate,Status")]
            Campaign input,
            HttpPostedFileBase campaignImage)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            if (input == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            // Lấy chiến dịch hiện tại từ database
            var campaign = database.Campaigns.Find(input.Id);

            if (campaign == null)
                return HttpNotFound();

            // Kiểm tra dữ liệu
            ValidateCampaign(input);
            ValidateCampaignImage(campaignImage);

            // Giữ ảnh hiện tại khi hiển thị lại form nếu có lỗi
            input.ImageUrl = campaign.ImageUrl;

            if (ModelState.IsValid)
            {
                campaign.CategoryId = input.CategoryId;
                campaign.Title = input.Title;
                campaign.Description = input.Description;
                campaign.TargetAmount = input.TargetAmount;
                campaign.StartDate = input.StartDate;
                campaign.EndDate = input.EndDate;
                campaign.Status = input.Status;

                // Chỉ thay ảnh nếu Admin chọn ảnh mới
                if (campaignImage != null &&
                    campaignImage.ContentLength > 0)
                {
                    campaign.ImageUrl = SaveCampaignImage(campaignImage);
                }

                // Không chọn ảnh mới thì giữ nguyên ImageUrl cũ
                database.SaveChanges();

                TempData["Success"] = "Đã cập nhật chiến dịch.";

                return RedirectToAction("Campaigns");
            }

            LoadCategories(input.CategoryId);
            return View(input);
        }

        // =========================
        // XÓA CHIẾN DỊCH - GET
        // =========================
        [HttpGet]
        public ActionResult DeleteCampaign(int? id)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            if (!id.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var campaign = database.Campaigns.Find(id.Value);

            if (campaign == null)
                return HttpNotFound();

            ViewBag.DonationCount =
                database.Donations.Count(d => d.CampaignId == campaign.Id);

            return View(campaign);
        }

        // =========================
        // XÓA CHIẾN DỊCH - POST
        // =========================
        [HttpPost, ActionName("DeleteCampaign")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteCampaignConfirmed(int id)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            var campaign = database.Campaigns.Find(id);

            if (campaign == null)
                return HttpNotFound();

            // Không cho xóa chiến dịch đã có quyên góp
            if (database.Donations.Any(d => d.CampaignId == id))
            {
                TempData["Error"] =
                    "Không thể xóa chiến dịch đã có khoản quyên góp. Bạn có thể chuyển trạng thái sang Đóng.";

                return RedirectToAction("Campaigns");
            }

            database.Campaigns.Remove(campaign);
            database.SaveChanges();

            TempData["Success"] = "Đã xóa chiến dịch.";

            return RedirectToAction("Campaigns");
        }

        // =========================
        // QUẢN LÝ QUYÊN GÓP
        // =========================
        public ActionResult Donations(string keyword, int? status)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            var query = database.Donations
                .Include(d => d.User)
                .Include(d => d.Campaign)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(d =>
                    d.TransactionCode.Contains(keyword) ||
                    d.User.FullName.Contains(keyword) ||
                    d.Campaign.Title.Contains(keyword));
            }

            if (status.HasValue)
                query = query.Where(d => d.Status == status.Value);

            ViewBag.Keyword = keyword;
            ViewBag.Status = status;

            ViewBag.ValidTotal =
                query.Where(d => d.Status == 1)
                     .Select(d => (decimal?)d.Amount)
                     .Sum() ?? 0m;

            return View(
                query.OrderByDescending(d => d.DonatedAt).ToList()
            );
        }

        // =========================
        // LOAD DANH MỤC
        // =========================
        private void LoadCategories(int? selectedId = null)
        {
            ViewBag.CategoryId = new SelectList(
                database.Categories.OrderBy(c => c.Name).ToList(),
                "Id",
                "Name",
                selectedId
            );
        }

        // =========================
        // KIỂM TRA DỮ LIỆU CHIẾN DỊCH
        // =========================
        private void ValidateCampaign(Campaign campaign)
        {
            if (campaign == null) return;

            if (string.IsNullOrWhiteSpace(campaign.Title))
            {
                ModelState.AddModelError(
                    "Title",
                    "Vui lòng nhập tên chiến dịch."
                );
            }

            if (string.IsNullOrWhiteSpace(campaign.Description))
            {
                ModelState.AddModelError(
                    "Description",
                    "Vui lòng nhập mô tả chiến dịch."
                );
            }

            if (campaign.TargetAmount <= 0)
            {
                ModelState.AddModelError(
                    "TargetAmount",
                    "Mục tiêu quyên góp phải lớn hơn 0."
                );
            }

            if (campaign.EndDate <= campaign.StartDate)
            {
                ModelState.AddModelError(
                    "EndDate",
                    "Ngày kết thúc phải sau ngày bắt đầu."
                );
            }

            if (!campaign.Status.HasValue ||
                (campaign.Status.Value != 0 &&
                 campaign.Status.Value != 1 &&
                 campaign.Status.Value != 2))
            {
                ModelState.AddModelError(
                    "Status",
                    "Trạng thái chiến dịch không hợp lệ."
                );
            }
        }

        // =========================
        // KIỂM TRA FILE ẢNH
        // =========================
        private void ValidateCampaignImage(HttpPostedFileBase image)
        {
            if (image == null || image.ContentLength <= 0)
                return;

            string extension =
                Path.GetExtension(image.FileName).ToLowerInvariant();

            string[] allowedExtensions =
            {
                ".jpg", ".jpeg", ".png", ".gif", ".webp"
            };

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    "ImageUrl",
                    "Chỉ chấp nhận ảnh JPG, JPEG, PNG, GIF hoặc WEBP."
                );
            }

            if (image.ContentLength > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(
                    "ImageUrl",
                    "Ảnh không được vượt quá 5 MB."
                );
            }
        }

        // =========================
        // LƯU FILE ẢNH VÀO SERVER
        // =========================
        private string SaveCampaignImage(HttpPostedFileBase image)
        {
            string folderPath =
                Server.MapPath("~/Content/Images/Campaigns/");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string extension =
                Path.GetExtension(image.FileName).ToLowerInvariant();

            string fileName =
                Guid.NewGuid().ToString("N") + extension;

            string fullPath =
                Path.Combine(folderPath, fileName);

            image.SaveAs(fullPath);

            return "/Content/Images/Campaigns/" + fileName;
        }

        // =========================
        // DISPOSE
        // =========================
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                database.Dispose();

            base.Dispose(disposing);
        }
    }
}
