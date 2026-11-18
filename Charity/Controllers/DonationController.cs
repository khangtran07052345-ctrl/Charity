
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using PagedList;
using Charity.Models;

namespace Charity.Controllers
{
    public class DonationController : Controller
    {
        private readonly CharityDBEntities db = new CharityDBEntities();

        // ===== CẤU HÌNH TÀI KHOẢN NHẬN TIỀN DEMO =====
        private const string BankCode = "MB";
        private const string BankName = "MB Bank";
        private const string BankAccountNo = "0123456789";
        private const string BankAccountName = "QUY TU THIEN CHARITY";
        private const string EWalletPhone = "0901234567";

        private static readonly string[] MoneyMethods =
        {
            "Chuyển khoản ngân hàng",
            "Ví MoMo (demo)",
            "ZaloPay (demo)"
        };

        private static readonly string[] DeliveryMethods =
        {
            "Tự mang đến",
            "Nhờ đến lấy",
            "Gửi bưu điện"
        };

        // =============================================
        // GET: TRANG QUYÊN GÓP
        // =============================================
        [HttpGet]
        public ActionResult Create(int campaignId, string type = "Money")
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

            var campaign = db.Campaigns.Find(campaignId);

            if (campaign == null)
                return HttpNotFound();

            string error = GetDonationError(campaign);

            if (error != null)
            {
                TempData["Error"] = error;

                return RedirectToAction(
                    "Details",
                    "Campaign",
                    new { id = campaignId }
                );
            }

            var donation = new Donation
            {
                CampaignId = campaignId,
                Amount = 100000m,
                IsAnonymous = false,
                DonationType = (type == "InKind") ? "InKind" : "Money",
                PaymentMethod = MoneyMethods[0],
                TransactionCode = NewTransferCode(campaignId)
            };

            PrepareViewBag(campaign);

            return View(donation);
        }

        // =============================================
        // POST: XÁC NHẬN QUYÊN GÓP
        // =============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(
            [Bind(Include = "CampaignId,Amount,PaymentMethod,Message,IsAnonymous,TransactionCode," +
                            "DonationType,ItemName,Quantity,Unit,EstimatedValue,DeliveryMethod,ContactPhone,PickupAddress,DeliverySubOption,PostalTrackingCode")]
            Donation donation,
            string DonationLetter)
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

            if (donation == null)
                return new HttpStatusCodeResult(400);

            var campaign = db.Campaigns.Find(donation.CampaignId);

            if (campaign == null)
                return HttpNotFound();

            string error = GetDonationError(campaign);

            if (error != null)
            {
                TempData["Error"] = error;

                return RedirectToAction(
                    "Details",
                    "Campaign",
                    new { id = campaign.Id }
                );
            }

            bool isInKind = donation.DonationType == "InKind";

            donation.DonationType = isInKind ? "InKind" : "Money";

            if (donation.Message != null && donation.Message.Length > 500)
            {
                ModelState.AddModelError(
                    "Message",
                    "Lời nhắn không được vượt quá 500 ký tự."
                );
            }

            if (isInKind)
                ValidateInKind(donation, DonationLetter);
            else
                ValidateMoney(donation);

            if (!ModelState.IsValid)
            {
                PrepareViewBag(campaign);
                return View(donation);
            }

            donation.UserId = Convert.ToInt32(Session["UserId"]);
            donation.DonatedAt = DateTime.Now;

            donation.DonationType = isInKind ? "InKind" : "Money";

            // =============================================
            // XỬ LÝ QUYÊN GÓP HIỆN VẬT
            // =============================================
            if (isInKind)
            {
                donation.Amount = 0m;
                donation.PaymentMethod = "Hiện vật";

                donation.TransactionCode =
                    "GOODS-" +
                    Guid.NewGuid().ToString("N")
                    .Substring(0, 12)
                    .ToUpperInvariant();

                donation.Status = 0;

                donation.ItemName = donation.ItemName.Trim();

                if (donation.DeliveryMethod == "Tự mang đến")
                {
                    donation.PickupAddress = null;
                    donation.PostalTrackingCode = null;

                    if (donation.DeliverySubOption == "Viết thư tham gia" &&
                        !string.IsNullOrWhiteSpace(DonationLetter))
                    {
                        string letterContent =
                            "[Thư lan tỏa yêu thương]: " +
                            DonationLetter.Trim();

                        donation.Message = string.IsNullOrEmpty(donation.Message)
                            ? letterContent
                            : donation.Message + " | " + letterContent;
                    }
                }
                else if (donation.DeliveryMethod == "Nhờ đến lấy")
                {
                    donation.DeliverySubOption = null;
                    donation.PostalTrackingCode = null;

                    if (donation.PickupAddress != null)
                        donation.PickupAddress = donation.PickupAddress.Trim();
                }
                else if (donation.DeliveryMethod == "Gửi bưu điện")
                {
                    donation.DeliverySubOption = null;
                    donation.PickupAddress = null;

                    if (donation.PostalTrackingCode != null)
                        donation.PostalTrackingCode =
                            donation.PostalTrackingCode.Trim();
                }
            }

            // =============================================
            // XỬ LÝ QUYÊN GÓP TIỀN
            // =============================================
            else
            {
                donation.DonationType = "Money";

                if (!IsValidTransferCode(donation.TransactionCode) ||
                    db.Donations.Any(d =>
                        d.TransactionCode == donation.TransactionCode))
                {
                    donation.TransactionCode =
                        NewTransferCode(donation.CampaignId);
                }

                // Quyên góp tiền theo luồng demo hiện tại
                donation.Status = 1;

                donation.ItemName = null;
                donation.Quantity = null;
                donation.Unit = null;
                donation.EstimatedValue = null;
                donation.DeliveryMethod = null;
                donation.ContactPhone = null;
                donation.PickupAddress = null;
                donation.DeliverySubOption = null;
                donation.PostalTrackingCode = null;
            }

            // =============================================
            // LƯU QUYÊN GÓP VÀO DATABASE
            // =============================================
            try
            {
                db.Donations.Add(donation);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                string errorMessage = ex.ToString();

                return Content(
                    "<pre>" +
                    Server.HtmlEncode(errorMessage) +
                    "</pre>"
                );
            }

            // =============================================
            // CẬP NHẬT TIẾN ĐỘ CHIẾN DỊCH
            // =============================================
            if (!isInKind)
            {
                decimal raisedAmount = db.Donations
                    .Where(d =>
                        d.CampaignId == campaign.Id &&
                        d.Status == 1)
                    .Select(d => (decimal?)d.Amount)
                    .Sum() ?? 0m;

                if (raisedAmount >= campaign.TargetAmount)
                {
                    campaign.Status = 2;
                    db.SaveChanges();
                }

                TempData["Success"] =
                    "Quyên góp thành công. Mã giao dịch: " +
                    donation.TransactionCode;
            }
            else
            {
                TempData["Success"] =
                    "Đã ghi nhận đăng ký quyên góp hiện vật (" +
                    donation.ItemName + "). Mã: " +
                    donation.TransactionCode +
                    ". Vui lòng chờ quản trị viên xác nhận.";
            }

            return RedirectToAction(
                "Details",
                "Campaign",
                new { id = campaign.Id }
            );
        }

        // =============================================
        // KIỂM TRA QUYÊN GÓP TIỀN
        // =============================================
        private void ValidateMoney(Donation d)
        {
            if (d.Amount <= 0)
            {
                ModelState.AddModelError(
                    "Amount",
                    "Số tiền quyên góp phải lớn hơn 0."
                );
            }
            else if (d.Amount > 1000000000000m)
            {
                ModelState.AddModelError(
                    "Amount",
                    "Số tiền vượt quá giới hạn demo."
                );
            }

            if (string.IsNullOrWhiteSpace(d.PaymentMethod) ||
                !MoneyMethods.Contains(d.PaymentMethod))
            {
                d.PaymentMethod = MoneyMethods[0];
            }
        }

        // =============================================
        // KIỂM TRA QUYÊN GÓP HIỆN VẬT
        // =============================================
        private void ValidateInKind(Donation d, string donationLetter)
        {
            if (string.IsNullOrWhiteSpace(d.ItemName))
            {
                ModelState.AddModelError(
                    "ItemName",
                    "Vui lòng nhập tên hiện vật."
                );
            }
            else if (d.ItemName.Length > 200)
            {
                ModelState.AddModelError(
                    "ItemName",
                    "Tên hiện vật tối đa 200 ký tự."
                );
            }

            if (d.Quantity == null || d.Quantity <= 0)
            {
                ModelState.AddModelError(
                    "Quantity",
                    "Số lượng phải lớn hơn 0."
                );
            }
            else if (d.Quantity > 1000000000m)
            {
                ModelState.AddModelError(
                    "Quantity",
                    "Số lượng quá lớn."
                );
            }

            if (string.IsNullOrWhiteSpace(d.Unit))
            {
                ModelState.AddModelError(
                    "Unit",
                    "Vui lòng chọn hoặc nhập đơn vị."
                );
            }
            else if (d.Unit.Length > 30)
            {
                ModelState.AddModelError(
                    "Unit",
                    "Đơn vị tối đa 30 ký tự."
                );
            }

            if (d.EstimatedValue != null &&
                (d.EstimatedValue < 0 ||
                 d.EstimatedValue > 1000000000000m))
            {
                ModelState.AddModelError(
                    "EstimatedValue",
                    "Giá trị ước tính không hợp lệ."
                );
            }

            if (string.IsNullOrWhiteSpace(d.DeliveryMethod) ||
                !DeliveryMethods.Contains(d.DeliveryMethod))
            {
                ModelState.AddModelError(
                    "DeliveryMethod",
                    "Vui lòng chọn hình thức giao hàng."
                );
            }

            if (d.DeliveryMethod == "Nhờ đến lấy")
            {
                if (string.IsNullOrWhiteSpace(d.PickupAddress))
                {
                    ModelState.AddModelError(
                        "PickupAddress",
                        "Vui lòng nhập địa chỉ để bên mình đến nhận."
                    );
                }
                else if (d.PickupAddress.Length > 300)
                {
                    ModelState.AddModelError(
                        "PickupAddress",
                        "Địa chỉ tối đa 300 ký tự."
                    );
                }
            }
            else if (d.DeliveryMethod == "Tự mang đến")
            {
                if (d.DeliverySubOption == "Viết thư tham gia" &&
                    !string.IsNullOrWhiteSpace(donationLetter) &&
                    donationLetter.Length > 1000)
                {
                    ModelState.AddModelError(
                        "Message",
                        "Nội dung bức thư không được vượt quá 1000 ký tự."
                    );
                }
            }
            else if (d.DeliveryMethod == "Gửi bưu điện")
            {
                if (d.PostalTrackingCode != null &&
                    d.PostalTrackingCode.Length > 100)
                {
                    ModelState.AddModelError(
                        "PostalTrackingCode",
                        "Mã vận đơn tối đa 100 ký tự."
                    );
                }
            }

            if (string.IsNullOrWhiteSpace(d.ContactPhone) ||
                !Regex.IsMatch(
                    d.ContactPhone.Trim(),
                    @"^0\d{9,10}$"))
            {
                ModelState.AddModelError(
                    "ContactPhone",
                    "Số điện thoại không hợp lệ (ví dụ 0901234567)."
                );
            }
            else
            {
                d.ContactPhone = d.ContactPhone.Trim();
            }
        }

        // =============================================
        // CHUẨN BỊ DỮ LIỆU CHO VIEW
        // =============================================
        private void PrepareViewBag(Campaign campaign)
        {
            ViewBag.Campaign = campaign;
            ViewBag.BankCode = BankCode;
            ViewBag.BankName = BankName;
            ViewBag.BankAccountNo = BankAccountNo;
            ViewBag.BankAccountName = BankAccountName;
            ViewBag.EWalletPhone = EWalletPhone;
            ViewBag.MoneyMethods = MoneyMethods;
            ViewBag.DeliveryMethods = DeliveryMethods;
        }

        // =============================================
        // TẠO MÃ GIAO DỊCH
        // =============================================
        private string NewTransferCode(int campaignId)
        {
            return "CHARITY" +
                campaignId +
                "U" +
                Convert.ToInt32(Session["UserId"]) +
                Guid.NewGuid().ToString("N")
                    .Substring(0, 6)
                    .ToUpperInvariant();
        }

        private bool IsValidTransferCode(string code)
        {
            return !string.IsNullOrWhiteSpace(code) &&
                Regex.IsMatch(
                    code,
                    @"^CHARITY[0-9]+U[0-9]+[A-Z0-9]{6}$"
                );
        }

        // =============================================
        // KIỂM TRA ĐIỀU KIỆN CHIẾN DỊCH
        // =============================================
        private string GetDonationError(Campaign campaign)
        {
            if (campaign.Status.HasValue &&
                campaign.Status.Value != 0)
            {
                return "Chiến dịch đã đóng hoặc không còn nhận quyên góp.";
            }

            if (campaign.StartDate.Date > DateTime.Today)
            {
                return "Chiến dịch chưa đến ngày bắt đầu. Vui lòng quay lại sau.";
            }

            if (campaign.EndDate.Date < DateTime.Today)
            {
                return "Chiến dịch đã hết hạn, không thể nhận quyên góp.";
            }

            decimal raisedAmount = db.Donations
                .Where(d =>
                    d.CampaignId == campaign.Id &&
                    d.Status == 1)
                .Select(d => (decimal?)d.Amount)
                .Sum() ?? 0m;

            if (raisedAmount >= campaign.TargetAmount)
            {
                return "Chiến dịch đã đạt mục tiêu, không nhận thêm quyên góp.";
            }

            return null;
        }

        // =============================================
        // LỊCH SỬ QUYÊN GÓP CÓ PHÂN TRANG
        // =============================================
        public ActionResult History(int page = 1)
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

            int userId = Convert.ToInt32(Session["UserId"]);

            int pageSize = 10;

            var query = db.Donations
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.DonatedAt);

            ViewBag.TotalAmount = query
                .Where(d => d.Status == 1)
                .Select(d => (decimal?)d.Amount)
                .Sum() ?? 0m;

            ViewBag.TotalDonations =
                query.Count(d => d.Status == 1);

            ViewBag.UserId = userId;

            if (page < 1)
                page = 1;

            var donations = query.ToPagedList(page, pageSize);

            return View(donations);
        }

        // =============================================
        // GIẢI PHÓNG DATABASE
        // =============================================
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }
}
