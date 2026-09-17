using System.Security.Claims;
using GenericTestWebApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Roles = "Admin")]
public class AdminAuditController(TestPrepDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Logs([FromQuery] string? search, [FromQuery] string? action, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 10, 100);
        var query = db.AdminAuditLogs.AsNoTracking().Join(db.Users.AsNoTracking(), x => x.AdminUserId, u => u.Id, (x,u) => new { x, adminName = u.FullName, adminEmail = u.Email });
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x => x.adminName.Contains(s) || x.adminEmail.Contains(s) || x.x.Action.Contains(s) || x.x.TargetType.Contains(s) || (x.x.Details != null && x.x.Details.Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(action) && action != "All") query = query.Where(x => x.x.Action == action);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.x.Id, adminName = x.adminName, adminEmail = x.adminEmail, x.x.Action, x.x.TargetType, x.x.TargetId, x.x.Details, x.x.CreatedAtUtc }).ToListAsync(ct);
        var actions = await db.AdminAuditLogs.AsNoTracking().Select(x => x.Action).Distinct().OrderBy(x => x).ToListAsync(ct);
        return Ok(new { items, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize), actions });
    }
}
