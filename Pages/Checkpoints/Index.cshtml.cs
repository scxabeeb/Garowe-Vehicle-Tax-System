using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Checkpoints;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public int EditId { get; set; }

    [BindProperty]
    public string EditName { get; set; } = string.Empty;

    public List<CheckpointRow> Checkpoints { get; set; } = new();

    public class CheckpointRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int UsersAssigned { get; set; }
    }

    // Lightweight user DTO for the assign/unassign modal
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsAssigned { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            TempData["Error"] = "Checkpoint name is required.";
            await LoadAsync();
            return Page();
        }

        var normalizedName = Name.Trim();

        var exists = await _context.Checkpoints
            .AnyAsync(c => c.Name.ToLower() == normalizedName.ToLower());

        if (exists)
        {
            TempData["Error"] = "Checkpoint already exists.";
            await LoadAsync();
            return Page();
        }

        _context.Checkpoints.Add(new Checkpoint
        {
            Name = normalizedName
        });

        await _context.SaveChangesAsync();
        TempData["Message"] = "Checkpoint created successfully.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        if (EditId <= 0 || string.IsNullOrWhiteSpace(EditName))
        {
            TempData["Error"] = "Valid checkpoint details are required.";
            return RedirectToPage();
        }

        var checkpoint = await _context.Checkpoints.FirstOrDefaultAsync(c => c.Id == EditId);
        if (checkpoint == null)
        {
            TempData["Error"] = "Checkpoint not found.";
            return RedirectToPage();
        }

        var normalizedName = EditName.Trim();

        var duplicate = await _context.Checkpoints
            .AnyAsync(c => c.Id != EditId && c.Name.ToLower() == normalizedName.ToLower());

        if (duplicate)
        {
            TempData["Error"] = "Another checkpoint already uses this name.";
            return RedirectToPage();
        }

        checkpoint.Name = normalizedName;
        await _context.SaveChangesAsync();

        TempData["Message"] = "Checkpoint updated successfully.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var checkpoint = await _context.Checkpoints.FirstOrDefaultAsync(c => c.Id == id);
        if (checkpoint == null)
        {
            TempData["Error"] = "Checkpoint not found.";
            return RedirectToPage();
        }

        var inUse = await _context.Users.AnyAsync(u => u.CheckpointId == id);
        if (inUse)
        {
            TempData["Error"] = "Cannot delete this checkpoint because users are assigned to it. Reassign or unassign users first.";
            return RedirectToPage();
        }

        _context.Checkpoints.Remove(checkpoint);
        await _context.SaveChangesAsync();

        TempData["Message"] = "Checkpoint deleted successfully.";
        return RedirectToPage();
    }

    // =======================
    // Assign / Unassign Users
    // =======================

    /// <summary>
    /// Returns the list of all users with an IsAssigned flag indicating
    /// whether each user is currently assigned to the given checkpoint.
    /// </summary>
    public async Task<IActionResult> OnGetAssignUsers(int id)
    {
        var checkpoint = await _context.Checkpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (checkpoint == null)
        {
            return NotFound(new { status = "error", message = "Checkpoint not found." });
        }

        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.Username)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Role = u.Role,
                IsAssigned = u.CheckpointId == id
            })
            .ToListAsync();

        return new OkObjectResult(new
        {
            status = "success",
            checkpointId = checkpoint.Id,
            checkpointName = checkpoint.Name,
            users
        });
    }

    /// <summary>
    /// Assigns the selected users to the checkpoint and unassigns
    /// (sets CheckpointId = null) any user currently on the checkpoint
    /// who is not in the selected list. Users may only belong to one
    /// checkpoint, so selecting a user here moves them to this checkpoint.
    /// </summary>
    public async Task<IActionResult> OnPostAssignUsers(int checkpointId, List<int> selectedUserIds)
    {
        var checkpoint = await _context.Checkpoints
            .FirstOrDefaultAsync(c => c.Id == checkpointId);

        if (checkpoint == null)
        {
            return NotFound(new { status = "error", message = "Checkpoint not found." });
        }

        var requestedIds = selectedUserIds ?? new List<int>();

        // Assign requested users to this checkpoint
        var usersToAssign = await _context.Users
            .Where(u => requestedIds.Contains(u.Id))
            .ToListAsync();

        foreach (var user in usersToAssign)
        {
            user.CheckpointId = checkpointId;
        }

        // Unassign (clear) any user currently on this checkpoint who wasn't selected
        var usersToUnassign = await _context.Users
            .Where(u => u.CheckpointId == checkpointId && !requestedIds.Contains(u.Id))
            .ToListAsync();

        foreach (var user in usersToUnassign)
        {
            user.CheckpointId = null;
        }

        await _context.SaveChangesAsync();

        TempData["Message"] = $"Checkpoint \"{checkpoint.Name}\" updated: {usersToAssign.Count} assigned, {usersToUnassign.Count} unassigned.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Checkpoints = await _context.Checkpoints
            .OrderBy(c => c.Name)
            .Select(c => new CheckpointRow
            {
                Id = c.Id,
                Name = c.Name,
                UsersAssigned = _context.Users.Count(u => u.CheckpointId == c.Id)
            })
            .ToListAsync();
    }
}
