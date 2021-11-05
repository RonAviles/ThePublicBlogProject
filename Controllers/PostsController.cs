using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TheBlogProject.Data;
using TheBlogProject.Models;
using TheBlogProject.Services;
using TheBlogProject.Enums;
using X.PagedList;
using Microsoft.AspNetCore.Authorization;

namespace TheBlogProject.Controllers
{
    public class PostsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISlugService _slugService;
        private readonly IImageService _imageService;
        private readonly UserManager<BlogUser> _userManager;
        private readonly BlogSearchService _blogSearchService;

        public PostsController(ApplicationDbContext context, ISlugService slugService, IImageService imageService, UserManager<BlogUser> userManager, BlogSearchService blogSearchService)
        {
            _context = context;
            _slugService = slugService;
            _imageService = imageService;
            _userManager = userManager;
            _blogSearchService = blogSearchService;
        }

        public async Task<IActionResult> SearchIndex(int? page, string searchTerm)
        {

            ViewData["SearchTerm"] = searchTerm;
            var pageNumber =  page ?? 1;
            var pageSize = 5;

            var posts = _blogSearchService.Search(searchTerm);

            return View(await posts.ToPagedListAsync(pageNumber, pageSize));

        }

        // GET: Posts
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Posts.Include(p => p.Blog).Include(p => p.BlogUser);
            return View(await applicationDbContext.ToListAsync());
        }

        //tagIndex
        public async Task<IActionResult> TagIndex(int? id, int? page, string tag)
        {
            if (id is null)
            {
                return NotFound();
            }

            var pageNumber = page ?? 1;
            var pageSize = 5;

            //var posts = _context.Posts.Where(p => p.BlogId == id).ToList();

            var posts = await _context.Posts.Where(p => p.Tags.Any(t => t.Text.ToLower() == tag ) && p.ReadyStatus == ReadyStatus.ProductionReady)
                .OrderByDescending(p => p.Created)
                .ToPagedListAsync(pageNumber, pageSize);

            return View("BlogPostIndex", posts);
        }

        //blog post index
        public async Task<IActionResult> BlogPostIndex(int? id, int? page)
        {
            if(id is null)
            {
                return NotFound();
            }

            var pageNumber = page ?? 1;
            var pageSize = 5;

            //var posts = _context.Posts.Where(p => p.BlogId == id).ToList();
            var posts = await _context.Posts.Where(p => p.BlogId == id && p.ReadyStatus == ReadyStatus.ProductionReady)
                .OrderByDescending(p => p.Created)
                .ToPagedListAsync(pageNumber, pageSize);

            return View(posts);
        }

        // GET: Posts/Details/5
        public async Task<IActionResult> Details(string slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                return NotFound();
            }

            var post = await _context.Posts
                .Include(p => p.Blog)
                .Include(p => p.BlogUser)
                .Include(p => p.Tags)
                .Include(p => p.Comments)
                .ThenInclude(c => c.BlogUser)
                .FirstOrDefaultAsync(m => m.Slug == slug);
            if (post == null)
            {
                return NotFound();
            }

            return View(post);
        }

        // GET: Posts/Create
        [Authorize(Roles = "Administrator")]
        public IActionResult Create()
        {
            ViewData["BlogId"] = new SelectList(_context.Blogs, "Id", "Name");
            ViewData["BlogUserId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: Posts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BlogId,Title,Abstract,Content,ReadyStatus,Image")] Post post, List<string> tagValues)
        {
            if (ModelState.IsValid)
            {
                post.Created = DateTime.Now;

                var authorId = _userManager.GetUserId(User);
                post.BlogUserId = authorId;


                //use the _imageService to store the incoming user specified image.
                post.ImageData = await _imageService.EncodeImageAsync(post.Image);
                post.ContentType = _imageService.ContentType(post.Image);

                //create slug and determind if it is unique
                var slug = _slugService.UrlFriendly(post.Title);

                //create a variable to store whether an error has occured
                var validationError = false;

                if (string.IsNullOrEmpty(slug))
                {
                    validationError = true;
                    ModelState.AddModelError("", "The Title you provided cannot be used as it results in an empty slug.");
                }

                //detect incoming duplicate Slugs
                else if (!_slugService.IsUnique(slug))
                {
                    validationError = true;
                    ModelState.AddModelError("Title", "The Title you provided cannot be used as it results in a duplicate.");
                }

                if(validationError)
                {
                    ViewData["TagValues"] = string.Join(",", tagValues);
                    ViewData["BlogId"] = new SelectList(_context.Blogs, "Id", "Name", post.BlogId);

                    return View(post);
                }

                post.Slug = slug;

                _context.Add(post);
                await _context.SaveChangesAsync();

                //How do I loop over the incoming list of string?
                foreach(var tagText in tagValues)
                {
                    _context.Add(new Tag()
                    {
                        PostId = post.Id,
                        BlogUserId = authorId,
                        Text = tagText
                    });
                }

                await _context.SaveChangesAsync();

                return RedirectToAction("BlogPostIndex", new { Id = post.BlogId });
            }
            ViewData["BlogId"] = new SelectList(_context.Blogs, "Id", "Name", post.BlogId);
            ViewData["TagValues"] = string.Join(",", tagValues);

            return View(post);
        }

        // GET: Posts/Edit/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Edit(string slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                return NotFound();
            }

            var post = await _context.Posts.Include(p => p.Tags).FirstOrDefaultAsync(p => p.Slug == slug);
            if (post == null)
            {
                return NotFound();
            }
            ViewData["BlogId"] = new SelectList(_context.Blogs, "Id", "Name", post.BlogId);
            ViewData["BlogUserId"] = new SelectList(_context.Users, "Id", "Id", post.BlogUserId);
            ViewData["TagValues"] = string.Join(",", post.Tags.Select(t => t.Text));
            return View(post);
        }

        // POST: Posts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,BlogId,Title,Abstract,Content,ReadyStatus")] Post post, IFormFile newImage, List<string> tagValues)
        {
            if (id != post.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    //original post
                    var newPost = await _context.Posts.Include(p => p.Tags).FirstOrDefaultAsync(p => p.Id == post.Id);

                    newPost.Updated = DateTime.Now;
                    newPost.Title = post.Title;
                    newPost.Abstract = post.Abstract;
                    newPost.Content = post.Content;
                    newPost.ReadyStatus = post.ReadyStatus;

                    var newSlug = _slugService.UrlFriendly(post.Title);
                    if(newSlug != newPost.Slug)
                    {
                        if(_slugService.IsUnique(newSlug))
                        {
                            newPost.Title = post.Title;
                            newPost.Slug = newSlug;
                        }
                        else
                        {
                            ModelState.AddModelError("Title", "This title cannot be used as it results in a duplicate slug.");
                            ViewData["BlogId"] = new SelectList(_context.Blogs, "Id", "Name", newPost.BlogId);
                            ViewData["BlogUserId"] = new SelectList(_context.Users, "Id", "Id", newPost.BlogUserId);
                            ViewData["TagValues"] = string.Join(",", newPost.Tags.Select(t => t.Text));
                            return View(post);
                        }
                    }

                    if (newImage is not null)
                    {
                        newPost.ImageData = await _imageService.EncodeImageAsync(newImage);
                        newPost.ContentType = _imageService.ContentType(newImage);                    
                    }

                    //remove all tags previously associated with this post
                    _context.Tags.RemoveRange(newPost.Tags);

                    //add new tags from the edit form
                    foreach (var tagText in tagValues)
                    {
                        _context.Add(new Tag()
                        {
                            PostId = post.Id,
                            BlogUserId = newPost.BlogUserId,
                            Text = tagText
                        });
                    }

                 
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PostExists(post.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                var slug = _slugService.UrlFriendly(post.Title);
                return RedirectToAction("Details", new { slug = slug });
            }
            ViewData["BlogId"] = new SelectList(_context.Blogs, "Id", "Description", post.BlogId);
            ViewData["BlogUserId"] = new SelectList(_context.Users, "Id", "Id", post.BlogUserId);
            return View(post);
        }

        // GET: Posts/Delete/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(string slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                return NotFound();
            }

            var post = await _context.Posts
                .Include(p => p.Blog)
                .Include(p => p.BlogUser)
                .FirstOrDefaultAsync(m => m.Slug == slug);
            if (post == null)
            {
                return NotFound();
            }

            return View(post);
        }

        // POST: Posts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var post = await _context.Posts.FindAsync(id);
            var blogId = post.BlogId;
            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();
            //return RedirectToAction(nameof(Index));

            return RedirectToAction("BlogPostIndex", new { Id = blogId });
        }

        private bool PostExists(int id)
        {
            return _context.Posts.Any(e => e.Id == id);
        }
    }
}
