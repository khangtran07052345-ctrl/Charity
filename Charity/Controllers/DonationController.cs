using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Charity.Models;

namespace Charity.Controllers
{
    public class DonationController : Controller
    {
        private readonly CharityDBEntities db = new CharityDBEntities();

        // ====== CẤU HÌNH TÀI KHOẢN NHẬN TIỀN (DEMO) - sửa theo ý nhóm ======
        private const string BankCode = "MB";                 // mã ngân hàng VietQR: MB, VCB, TCB, ACB...
        private const string BankName = "MB Bank";
        private const string BankAccountNo = "0123456789";    // số tài khoản demo
        private const string BankAccountName = "QUY TU THIEN CHARITY";
        private const string EWalletPhone = "0901234567";     // số ví demo

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

        // GET: Trang quyên góp
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
                return RedirectToAction("Details", "Campaign", new { id = campaignId });
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

        // POST: Xác nhận quyên góp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(
            [Bind(Include = "CampaignId,Amount,PaymentMethod,Message,IsAnonymous,TransactionCode," +
                            "DonationType,ItemName,Quantity,Unit,EstimatedValue,DeliveryMethod,ContactPhone,PickupAddress")]
            Donation donation)
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
                return RedirectToAction("Details", "Campaign", new { id = campaign.Id });
            }

            bool isInKind = donation.DonationType == "InKind";
            donation.DonationType = isInKind ? "InKind" : "Money";

            if (donation.Message != null && donation.Message.Length > 500)
                ModelState.AddModelError("Message", "Lời nhắn không được vượt quá 500 ký tự.");

            if (isInKind)
                ValidateInKind(donation);
            else
                ValidateMoney(donation);

            if (!ModelState.IsValid)
            {
                PrepareViewBag(campaign);
                return View(donation);
            }

            donation.UserId = Convert.ToInt32(Session["UserId"]);
            donation.DonatedAt = DateTime.Now;

            // Đảm bảo loại quyên góp luôn có giá trị hợp lệ
            donation.DonationType = isInKind ? "InKind" : "Money";

            if (isInKind)
            {
                donation.Amount = 0m;                       // hiện vật không cộng vào tổng tiền
                donation.PaymentMethod = "Hiện vật";
                donation.TransactionCode = "GOODS-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
                donation.Status = 0;                        // 0 = chờ Admin xác nhận đã nhận hàng
                donation.ItemName = donation.ItemName.Trim();
            }
            else
            {
                donation.DonationType = "Money";

                if (!IsValidTransferCode(donation.TransactionCode) ||
                    db.Donations.Any(d => d.TransactionCode == donation.TransactionCode))
                {
                    donation.TransactionCode = NewTransferCode(donation.CampaignId);
                }

                donation.Status = 1;

                donation.ItemName = null;
                donation.Quantity = null;
                donation.Unit = null;
                donation.EstimatedValue = null;
                donation.DeliveryMethod = null;
                donation.ContactPhone = null;
                donation.PickupAddress = null;
            }

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

            if (!isInKind)
            {
                decimal raisedAmount = db.Donations
                    .Where(d => d.CampaignId == campaign.Id && d.Status == 1)
                    .Select(d => (decimal?)d.Amount)
                    .Sum() ?? 0m;

                if (raisedAmount >= campaign.TargetAmount)
                {
                    campaign.Status = 2;
                    db.SaveChanges();
                }

                TempData["Success"] = "Quyên góp thành công. Mã giao dịch: " + donation.TransactionCode;
            }
            else
            {
                TempData["Success"] =
                    "Đã ghi nhận đăng ký quyên góp hiện vật (" + donation.ItemName + "). " +
                    "Mã: " + donation.TransactionCode + ". Vui lòng chờ quản trị viên xác nhận.";
            }

            return RedirectToAction("Details", "Campaign", new { id = campaign.Id });
        }

        // ---------- Kiểm tra dữ liệu ----------
        private void ValidateMoney(Donation d)
        {
            if (d.Amount <= 0)
                ModelState.AddModelError("Amount", "Số tiền quyên góp phải lớn hơn 0.");
            else if (d.Amount > 1000000000000m)
                ModelState.AddModelError("Amount", "Số tiền vượt quá giới hạn demo.");

            if (string.IsNullOrWhiteSpace(d.PaymentMethod) || !MoneyMethods.Contains(d.PaymentMethod))
                d.PaymentMethod = MoneyMethods[0];
        }

        private void ValidateInKind(Donation d)
        {
            if (string.IsNullOrWhiteSpace(d.ItemName))
                ModelState.AddModelError("ItemName", "Vui lòng nhập tên hiện vật.");
            else if (d.ItemName.Length > 200)
                ModelState.AddModelError("ItemName", "Tên hiện vật tối đa 200 ký tự.");

            if (d.Quantity == null || d.Quantity <= 0)
                ModelState.AddModelError("Quantity", "Số lượng phải lớn hơn 0.");
            else if (d.Quantity > 1000000000m)
                ModelState.AddModelError("Quantity", "Số lượng quá lớn.");

            if (string.IsNullOrWhiteSpace(d.Unit))
                ModelState.AddModelError("Unit", "Vui lòng chọn hoặc nhập đơn vị.");
            else if (d.Unit.Length > 30)
                ModelState.AddModelError("Unit", "Đơn vị tối đa 30 ký tự.");

            if (d.EstimatedValue != null && (d.EstimatedValue < 0 || d.EstimatedValue > 1000000000000m))
                ModelState.AddModelError("EstimatedValue", "Giá trị ước tính không hợp lệ.");

            if (string.IsNullOrWhiteSpace(d.DeliveryMethod) || !DeliveryMethods.Contains(d.DeliveryMethod))
                ModelState.AddModelError("DeliveryMethod", "Vui lòng chọn hình thức giao hàng.");

            if (string.IsNullOrWhiteSpace(d.ContactPhone) ||
                !Regex.IsMatch(d.ContactPhone.Trim(), @"^0\d{9,10}$"))
                ModelState.AddModelError("ContactPhone", "Số điện thoại không hợp lệ (ví dụ 0901234567).");
            else
                d.ContactPhone = d.ContactPhone.Trim();

            if (d.DeliveryMethod == "Nhờ đến lấy" && string.IsNullOrWhiteSpace(d.PickupAddress))
                ModelState.AddModelError("PickupAddress", "Vui lòng nhập địa chỉ để bên mình đến nhận.");

            if (d.PickupAddress != null && d.PickupAddress.Length > 300)
                ModelState.AddModelError("PickupAddress", "Địa chỉ tối đa 300 ký tự.");
        }

        // ---------- Hỗ trợ ----------
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

        private string NewTransferCode(int campaignId)
        {
            // Ví dụ: CHARITY5U1005A1B2C3 (chỉ chữ + số để QR/ngân hàng không lỗi)
            return "CHARITY" + campaignId + "U" + Convert.ToInt32(Session["UserId"]) +
                   Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
        }

        private bool IsValidTransferCode(string code)
        {
            return !string.IsNullOrWhiteSpace(code) &&
                   Regex.IsMatch(code, @"^CHARITY[0-9]+U[0-9]+[A-Z0-9]{6}$");
        }

        private string GetDonationError(Campaign campaign)
        {
            if (campaign.Status.HasValue && campaign.Status.Value != 0)
                return "Chiến dịch đã đóng hoặc không còn nhận quyên góp.";

            if (campaign.StartDate.Date > DateTime.Today)
                return "Chiến dịch chưa đến ngày bắt đầu. Vui lòng quay lại sau.";

            if (campaign.EndDate.Date < DateTime.Today)
                return "Chiến dịch đã hết hạn, không thể nhận quyên góp.";

            decimal raisedAmount = db.Donations
                .Where(d => d.CampaignId == campaign.Id && d.Status == 1)
                .Select(d => (decimal?)d.Amount)
                .Sum() ?? 0m;

            if (raisedAmount >= campaign.TargetAmount)
                return "Chiến dịch đã đạt mục tiêu, không nhận thêm quyên góp.";

            return null;
        }

        // Lịch sử quyên góp (giữ nguyên như cũ)
        public ActionResult History()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

            int userId = Convert.ToInt32(Session["UserId"]);

            var donations = db.Donations
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.DonatedAt)
                .ToList();

            ViewBag.UserId = userId;
            ViewBag.TotalAmount = donations.Where(d => d.Status == 1).Sum(d => d.Amount);
            ViewBag.TotalDonations = donations.Count(d => d.Status == 1);

            return View(donations);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }
}