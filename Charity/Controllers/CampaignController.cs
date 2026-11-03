using Charity.Models;
using Charity.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Charity.Controllers
{
    public class CampaignController : Controller
    {
        private CharityDBEntities db = new CharityDBEntities();

        // /Campaign?keyword=lũ&status=ongoing
        public ActionResult Index(string keyword, string status)
        {
            var service = new CampaignService(db);
            var list = service.Search(keyword, status);

            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            return View(list);
        }

        public ActionResult Details(int id)
        {
            var service = new CampaignService(db);
            var campaign = service.GetById(id);
            if (campaign == null) return HttpNotFound();
            return View(campaign);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}