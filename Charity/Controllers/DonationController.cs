using System;
using System.Linq;
using System.Web.Mvc;
using Charity.Models;

namespace Charity.Controllers
{
    public class DonationController : Controller
    {
        private readonly CharityDBEntities db = new CharityDBEntities();

        // Hiển thị trang quyên góp
        [HttpGet]
        public ActionResult Create(int campaignId)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var campaign = db.Campaigns.Find(campaignId);

            if (campaign == null)
            {
                return HttpNotFound();
            }

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

            ViewBag.Campaign = campaign;

            var donation = new Donation
            {
                CampaignId = campaignId,
                Amount = 10000m,
                IsAnonymous = false
            };

            return View(donation);
        }

        // Xử lý khi xác nhận quyên góp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(
            [Bind(Include = "CampaignId,Amount,PaymentMethod,Message,IsAnonymous")]
            Donation donation)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (donation == null)
            {
                return new HttpStatusCodeResult(400);
            }

            var campaign = db.Campaigns.Find(donation.CampaignId);

            if (campaign == null)
            {
                return HttpNotFound();
            }

            // Kiểm tra lại trạng thái chiến dịch trước khi lưu
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

            // Kiểm tra số tiền quyên góp
            if (donation.Amount <= 0)
            {
                ModelState.AddModelError(
                    "Amount",
                    "Số tiền quyên góp phải lớn hơn 0."
                );
            }

            if (donation.Amount > 1000000000000m)
            {
                ModelState.AddModelError(
                    "Amount",
                    "Số tiền vượt quá giới hạn demo."
                );
            }

            if (string.IsNullOrWhiteSpace(donation.PaymentMethod))
            {
                donation.PaymentMethod = "Mô phỏng";
            }

            if (donation.Message != null && donation.Message.Length > 500)
            {
                ModelState.AddModelError(
                    "Message",
                    "Lời nhắn không được vượt quá 500 ký tự."
                );
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Campaign = campaign;
                return View(donation);
            }

            // Tạo giao dịch quyên góp mô phỏng
            donation.UserId = Convert.ToInt32(Session["UserId"]);

            donation.TransactionCode =
                "DEMO-" +
                Guid.NewGuid().ToString("N")
                    .Substring(0, 12)
                    .ToUpperInvariant();

            donation.DonatedAt = DateTime.Now;

            // 1 = Giao dịch hợp lệ
            donation.Status = 1;

            db.Donations.Add(donation);
            db.SaveChanges();

            // Tính tổng tiền quyên góp hợp lệ của chiến dịch
            decimal raisedAmount = db.Donations
                .Where(d =>
                    d.CampaignId == campaign.Id &&
                    d.Status == 1)
                .Select(d => (decimal?)d.Amount)
                .Sum() ?? 0m;

            // Nếu đạt mục tiêu thì chuyển chiến dịch sang trạng thái hoàn thành
            if (raisedAmount >= campaign.TargetAmount)
            {
                campaign.Status = 2;
                db.SaveChanges();
            }

            TempData["Success"] =
                "Quyên góp mô phỏng thành công. Mã giao dịch: " +
                donation.TransactionCode;

            return RedirectToAction(
                "Details",
                "Campaign",
                new { id = campaign.Id }
            );
        }

        // Trả về thông báo nếu chiến dịch không thể nhận quyên góp
        private string GetDonationError(Campaign campaign)
        {
            // 0 = Đang hoạt động
            // 1 = Hoàn thành mục tiêu
            // 2 = Đóng chiến dịch
            if (campaign.Status.HasValue && campaign.Status.Value != 0)
            {
                return "Chiến dịch đã đóng hoặc không còn nhận quyên góp.";
            }

            // Chưa đến ngày bắt đầu
            if (campaign.StartDate.Date > DateTime.Today)
            {
                return "Chiến dịch chưa đến ngày bắt đầu. Vui lòng quay lại sau.";
            }

            // Đã qua ngày kết thúc
            if (campaign.EndDate.Date < DateTime.Today)
            {
                return "Chiến dịch đã hết hạn, không thể nhận quyên góp.";
            }

            // Tính tổng tiền đã nhận hợp lệ
            decimal raisedAmount = db.Donations
                .Where(d =>
                    d.CampaignId == campaign.Id &&
                    d.Status == 1)
                .Select(d => (decimal?)d.Amount)
                .Sum() ?? 0m;

            // Đã đạt mục tiêu
            if (raisedAmount >= campaign.TargetAmount)
            {
                return "Chiến dịch đã đạt mục tiêu, không nhận thêm quyên góp.";
            }

            return null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}