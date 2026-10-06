using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberLms.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Policy = "Admin")]
public abstract class AdminController : CyberLms.Web.Controllers.AppController { }
