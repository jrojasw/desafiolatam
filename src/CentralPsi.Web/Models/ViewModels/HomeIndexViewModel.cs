using CentralPsi.Web.Models.Entities;

namespace CentralPsi.Web.Models.ViewModels;

public class HomeIndexViewModel
{
    public List<SlideImage> Slides { get; set; } = new();
    public List<NewsArticle> News { get; set; } = new();
}
