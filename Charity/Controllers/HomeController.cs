
using System;
using System.Linq;
using System.Web.Mvc;
using Charity.Models;
using Charity.Services;

public class HomeController : Controller
{
    private CharityDBEntities db = new CharityDBEntities();

    // TRANG CHỦ
    public ActionResult Index()
    {
        try
        {
            var service = new CampaignService(db);

            var featured = service.Search(null, "ongoing")
                                  .OrderByDescending(x => x.StartDate)
                                  .Take(4)
                                  .ToList();

            return View(featured);
        }
        catch (Exception ex)
        {
            // Ghi lỗi để tiện kiểm tra trong Visual Studio
            System.Diagnostics.Debug.WriteLine(
                "Lỗi tải trang chủ: " + ex.ToString()
            );

            // Trả về danh sách rỗng để trang chủ vẫn hiển thị
            return View(new System.Collections.Generic.List<CampaignViewModel>());
        }
    }

    // TRANG GIỚI THIỆU
    public ActionResult GioiThieu()
    {
        return View();
    }

    // TRANG LIÊN HỆ
    public ActionResult LienHe()
    {
        return View();
    }

    // GIẢI PHÓNG KẾT NỐI DATABASE
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            db.Dispose();
        }

        base.Dispose(disposing);
    }
}
