
using Charity.Models;
using Charity.Services;
using System;
using System.Collections.Generic;
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

        // =========================
        // KIỂM TRA QUYỀN ADMIN
        // =========================
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

            // Dữ liệu biểu đồ chiến dịch
            var campaignServiceList = new CampaignService(database)
                .Search()
                .OrderByDescending(c => c.EndDate)
                .ToList();

            ViewBag.CampaignNames =
                campaignServiceList.Select(c => c.Title).ToList();

            ViewBag.CampaignAmounts =
                campaignServiceList.Select(c => c.RaisedAmount).ToList();

            // Dữ liệu thống kê hiện vật hợp lệ
            var inKindList = validDonations
                .Where(d => d.DonationType == "Hiện vật" || d.ItemName != null)
                .ToList();

            int quanAo = inKindList
                .Where(d => d.ItemName != null &&
                            d.ItemName.ToLower().Contains("quần áo"))
                .Sum(d => (int?)d.Quantity) ?? 0;

            int mi = inKindList
                .Where(d => d.ItemName != null &&
                            (d.ItemName.ToLower().Contains("mì") ||
                             d.ItemName.ToLower().Contains("mỳ")))
                .Sum(d => (int?)d.Quantity) ?? 0;

            int gao = inKindList
                .Where(d => d.ItemName != null &&
                            d.ItemName.ToLower().Contains("gạo"))
                .Sum(d => (int?)d.Quantity) ?? 0;

            int aoAm = inKindList
                .Where(d => d.ItemName != null &&
                            d.ItemName.ToLower().Contains("áo ấm"))
                .Sum(d => (int?)d.Quantity) ?? 0;

            int sachVo = inKindList
                .Where(d => d.ItemName != null &&
                            (d.ItemName.ToLower().Contains("sách") ||
                             d.ItemName.ToLower().Contains("vở")))
                .Sum(d => (int?)d.Quantity) ?? 0;

            ViewBag.InKindData =
                new List<int> { quanAo, mi, gao, aoAm, sachVo };

            ViewBag.Campaigns = campaignServiceList;

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

            var campaign = database.Campaigns.Find(input.Id);

            if (campaign == null)
                return HttpNotFound();

            ValidateCampaign(input);
            ValidateCampaignImage(campaignImage);

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

                if (campaignImage != null &&
                    campaignImage.ContentLength > 0)
                {
                    campaign.ImageUrl = SaveCampaignImage(campaignImage);
                }

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
        // DANH SÁCH QUYÊN GÓP
        // =========================
        public ActionResult Donations(string keyword, int? status, string type)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            var query = database.Donations
                .Include(d => d.User)
                .Include(d => d.Campaign)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();

                query = query.Where(d =>
                    d.TransactionCode.Contains(keyword) ||
                    d.User.FullName.Contains(keyword) ||
                    d.Campaign.Title.Contains(keyword) ||
                    d.ItemName.Contains(keyword));
            }

            if (status.HasValue)
                query = query.Where(d => d.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(d => d.DonationType == type);

            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            ViewBag.Type = type;

            ViewBag.ValidTotal =
                query.Where(d => d.Status == 1)
                     .Select(d => (decimal?)d.Amount)
                     .Sum() ?? 0m;

            return View(
                query.OrderByDescending(d => d.DonatedAt).ToList()
            );
        }

        // =========================
        // CHI TIẾT QUYÊN GÓP
        // =========================
        [HttpGet]
        public ActionResult DetailsDonation(int? id)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            if (!id.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var donation = database.Donations
                .Include(d => d.User)
                .Include(d => d.Campaign)
                .FirstOrDefault(d => d.Id == id.Value);

            if (donation == null)
                return HttpNotFound();

            return View(donation);
        }

        // =========================
        // XÁC NHẬN QUYÊN GÓP HIỆN VẬT
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmDonation(int id)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            var donation = database.Donations.Find(id);

            if (donation == null)
                return HttpNotFound();

            if (donation.Status == 0)
            {
                donation.Status = 1;
                database.SaveChanges();

                TempData["Success"] =
                    "Đã xác nhận khoản quyên góp " +
                    donation.TransactionCode + ".";
            }

            return RedirectToAction("Donations");
        }

        // =========================
        // TỪ CHỐI QUYÊN GÓP
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectDonation(int id)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            var donation = database.Donations.Find(id);

            if (donation == null)
                return HttpNotFound();

            if (donation.Status == 0)
            {
                donation.Status = 2;
                database.SaveChanges();

                TempData["Success"] =
                    "Đã từ chối khoản quyên góp " +
                    donation.TransactionCode + ".";
            }

            return RedirectToAction("Donations");
        }

        // =========================
        // XÓA QUYÊN GÓP - GET
        // =========================
        [HttpGet]
        public ActionResult DeleteDonation(int? id)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            if (!id.HasValue)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var donation = database.Donations
                .Include(d => d.User)
                .Include(d => d.Campaign)
                .FirstOrDefault(d => d.Id == id.Value);

            if (donation == null)
                return HttpNotFound();

            if (donation.Status == 1)
            {
                TempData["Error"] =
                    "Không thể xóa giao dịch quyên góp đã hợp lệ.";

                return RedirectToAction("Donations");
            }

            if (donation.Status != 0 && donation.Status != 2)
            {
                TempData["Error"] =
                    "Trạng thái giao dịch không hợp lệ, không thể xóa.";

                return RedirectToAction("Donations");
            }

            return View(donation);
        }

        // =========================
        // XÓA QUYÊN GÓP - POST
        // =========================
        [HttpPost]
        [ActionName("DeleteDonation")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteDonationConfirmed(int id)
        {
            var denied = RequireAdmin();
            if (denied != null) return denied;

            var donation = database.Donations.Find(id);

            if (donation == null)
                return HttpNotFound();

            // Chặn xóa giao dịch hợp lệ
            if (donation.Status == 1)
            {
                TempData["Error"] =
                    "Không thể xóa giao dịch đã hợp lệ.";

                return RedirectToAction("Donations");
            }

            // Chỉ cho xóa trạng thái 0 hoặc 2
            if (donation.Status != 0 && donation.Status != 2)
            {
                TempData["Error"] =
                    "Trạng thái giao dịch không hợp lệ, không thể xóa.";

                return RedirectToAction("Donations");
            }

            string transactionCode = donation.TransactionCode;

            database.Donations.Remove(donation);
            database.SaveChanges();

            TempData["Success"] =
                "Đã xóa giao dịch " + transactionCode + ".";

            return RedirectToAction("Donations");
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
        // LƯU FILE ẢNH
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

        // =============================================
        // QUẢN LÝ NGƯỜI DÙNG
        // =============================================

        // GET: Admin/Users
        public ActionResult Users(string keyword, string role)
        {
            if (Session["UserId"] == null ||
                (Session["Role"] as string) != "Admin")
            {
                return RedirectToAction("Index", "Home");
            }

            var users = database.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();

                users = users.Where(u =>
                    u.FullName.Contains(keyword) ||
                    u.Email.Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                users = users.Where(u => u.Role == role);
            }

            ViewBag.Keyword = keyword;
            ViewBag.Role = role;

            return View(users.OrderBy(u => u.Id).ToList());
        }

        // GET: Admin/DetailsUser/5
        public ActionResult DetailsUser(int id)
        {
            if (Session["UserId"] == null ||
                (Session["Role"] as string) != "Admin")
            {
                return RedirectToAction("Index", "Home");
            }

            var user = database.Users.Find(id);

            if (user == null)
                return HttpNotFound();

            return View(user);
        }

        // GET: Admin/CreateUser
        public ActionResult CreateUser()
        {
            if (Session["UserId"] == null ||
                (Session["Role"] as string) != "Admin")
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // POST: Admin/CreateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateUser(
            string FullName,
            string Email,
            string Password,
            string PhoneNumber,
            string Role)
        {
            if (Session["UserId"] == null ||
                (Session["Role"] as string) != "Admin")
            {
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ họ tên, email và mật khẩu.";
                return View();
            }

            Email = Email.Trim();

            if (database.Users.Any(u => u.Email == Email))
            {
                ViewBag.Error = "Email này đã tồn tại.";
                return View();
            }

            if (Role != "User" && Role != "Admin")
            {
                ViewBag.Error = "Vai trò không hợp lệ.";
                return View();
            }

            var user = new User
            {
                FullName = FullName.Trim(),
                Email = Email,
                PasswordHash = Password,
                PhoneNumber = PhoneNumber,
                Role = Role,
                CreatedAt = DateTime.Now
            };

            database.Users.Add(user);
            database.SaveChanges();

            TempData["Success"] = "Thêm người dùng thành công.";
            return RedirectToAction("Users");
        }

        // GET: Admin/EditUser/5
        public ActionResult EditUser(int id)
        {
            if (Session["UserId"] == null ||
                (Session["Role"] as string) != "Admin")
            {
                return RedirectToAction("Index", "Home");
            }

            var user = database.Users.Find(id);

            if (user == null)
                return HttpNotFound();

            return View(user);
        }

        // POST: Admin/EditUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditUser(
            int Id,
            string FullName,
            string Email,
            string PhoneNumber,
            string Role,
            string Password)
        {
            if (Session["UserId"] == null ||
                (Session["Role"] as string) != "Admin")
            {
                return RedirectToAction("Index", "Home");
            }

            var user = database.Users.Find(Id);

            if (user == null)
                return HttpNotFound();

            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email))
            {
                ViewBag.Error = "Họ tên và email không được để trống.";
                return View(user);
            }

            Email = Email.Trim();

            if (database.Users.Any(u =>
                u.Email == Email && u.Id != Id))
            {
                ViewBag.Error = "Email này đã được tài khoản khác sử dụng.";
                return View(user);
            }

            if (Role != "User" && Role != "Admin")
            {
                ViewBag.Error = "Vai trò không hợp lệ.";
                return View(user);
            }

            user.FullName = FullName.Trim();
            user.Email = Email;
            user.PhoneNumber = PhoneNumber;
            user.Role = Role;

            // Chỉ đổi mật khẩu khi Admin nhập mật khẩu mới.
            if (!string.IsNullOrWhiteSpace(Password))
            {
                user.PasswordHash = Password;
            }

            database.SaveChanges();

            TempData["Success"] = "Cập nhật người dùng thành công.";
            return RedirectToAction("Users");
        }

        // GET: Admin/DeleteUser/5
        public ActionResult DeleteUser(int id)
        {
            if (Session["UserId"] == null ||
                (Session["Role"] as string) != "Admin")
            {
                return RedirectToAction("Index", "Home");
            }

            var user = database.Users.Find(id);

            if (user == null)
                return HttpNotFound();

            return View(user);
        }

        // POST: Admin/DeleteUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteUserConfirmed(int id)
        {
            if (Session["UserId"] == null ||
                (Session["Role"] as string) != "Admin")
            {
                return RedirectToAction("Index", "Home");
            }

            int currentUserId = Convert.ToInt32(Session["UserId"]);

            if (id == currentUserId)
            {
                TempData["Error"] = "Bạn không thể xóa tài khoản đang đăng nhập.";
                return RedirectToAction("Users");
            }

            var user = database.Users.Find(id);

            if (user == null)
                return HttpNotFound();

            if (user.Donations != null && user.Donations.Any())
            {
                TempData["Error"] =
                    "Không thể xóa tài khoản đã có lịch sử quyên góp.";
                return RedirectToAction("Users");
            }

            database.Users.Remove(user);
            database.SaveChanges();

            TempData["Success"] = "Xóa người dùng thành công.";
            return RedirectToAction("Users");
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
