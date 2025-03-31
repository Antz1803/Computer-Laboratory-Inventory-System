using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DNTS_CLIS.Data;
using DNTS_CLIS.Models;

namespace DNTS_CLIS.Controllers
{
    public class TrackRecordsController : Controller
    {
        private readonly DNTS_CLISContext _context;

        public TrackRecordsController(DNTS_CLISContext context)
        {
            _context = context;
        }

        // GET: TrackRecords
        public async Task<IActionResult> Index()
        {
            return View(await _context.TrackRecords.ToListAsync());
        }

        // GET: TrackRecords/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trackRecords = await _context.TrackRecords
                .FirstOrDefaultAsync(m => m.Id == id);
            if (trackRecords == null)
            {
                return NotFound();
            }

            return View(trackRecords);
        }

        // GET: TrackRecords/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: TrackRecords/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,TrackNo,ReceiverName,CreatedDate")] TrackRecords trackRecords)
        {
            if (ModelState.IsValid)
            {
                _context.Add(trackRecords);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(trackRecords);
        }

        // GET: TrackRecords/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trackRecords = await _context.TrackRecords.FindAsync(id);
            if (trackRecords == null)
            {
                return NotFound();
            }
            return View(trackRecords);
        }

        // POST: TrackRecords/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,TrackNo,ReceiverName,CreatedDate")] TrackRecords trackRecords)
        {
            if (id != trackRecords.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(trackRecords);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TrackRecordsExists(trackRecords.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(trackRecords);
        }

        // GET: TrackRecords/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trackRecords = await _context.TrackRecords
                .FirstOrDefaultAsync(m => m.Id == id);
            if (trackRecords == null)
            {
                return NotFound();
            }

            return View(trackRecords);
        }

        // POST: TrackRecords/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var trackRecords = await _context.TrackRecords.FindAsync(id);
            if (trackRecords != null)
            {
                _context.TrackRecords.Remove(trackRecords);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TrackRecordsExists(int id)
        {
            return _context.TrackRecords.Any(e => e.Id == id);
        }
    }
}
