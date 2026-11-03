using Charity.Models;
using Charity.Services;
using System.Linq;
using System.Web.Mvc;

public class HomeController : Controller
{
    private CharityDBEntities db = new CharityDBEntities();

    public ActionResult Index()
    {
        var service = new CampaignService(db);
        // Lấy 4 chiến dịch nổi bật: ưu tiên chiến dịch đang diễn ra, mới nhất
        var featured = service.Search(null, "ongoing")
                              .OrderByDescending(x => x.StartDate)
                              .Take(4)
                              .ToList();
        return View(featured);
    }
    public ActionResult GioiThieu()
    {
        return View();
    }
    public ActionResult LienHe()
    {
        return View();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) db.Dispose();
        base.Dispose(disposing);
    }
}