using AspNetCore.Sass.Sample.Models;
using AspNetCore.SaSS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AspNetCore.Sass.Sample.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        //private readonly ISassCompiler _sassCompiler;

        public HomeController(ILogger<HomeController> logger/*, ISassCompiler sassCompiler*/)
        {
            _logger = logger;
            //_sassCompiler = sassCompiler;
        }

        public IActionResult Index()
        {
            //_sassCompiler.Compile();
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
