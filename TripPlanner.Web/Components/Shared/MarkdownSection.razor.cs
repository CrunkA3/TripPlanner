// ------------------------------------------------------------------------
// This file is licensed to you under the MIT License.
// ------------------------------------------------------------------------

using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace TripPlanner.Web.Components.Shared;

public partial class MarkdownSection : FluentComponentBase
{
    // private IJSObjectReference _jsModule = default!;
    private bool _markdownChanged = false;
    private string? _content;
    private string? _fromAsset;

    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = default!;


    /// <summary>
    /// Gets or sets the Markdown content
    /// </summary>
    [Parameter]
    public string? Content
    {
        get => _content;
        set
        {
            if (_content is not null && !_content.Equals(value))
            {
                _markdownChanged = true;
            }
            _content = value;
        }
    }

    /// <summary>
    /// Gets or sets asset to read the Markdown from
    /// </summary>
    [Parameter]
    public string? FromAsset
    {
        get => _fromAsset;
        set
        {
            if (_fromAsset is not null && !_fromAsset.Equals(value))
            {
                _markdownChanged = true;
            }
            _fromAsset = value;
        }
    }

    [Parameter]
    public EventCallback OnContentConverted { get; set; }

    public MarkupString HtmlContent { get; private set; }

    protected override void OnInitialized()
    {
        if (Content is null && string.IsNullOrEmpty(FromAsset))
        {
            throw new ArgumentException("You need to provide either Content or FromAsset parameter");
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        //if (firstRender)
        //{
        //    // import code for highlighting code blocks
        //    _jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>("import",
        //        "./_content/FluentUI.Demo.Shared/Components/MarkdownSection.razor.js");
        //}

        if (firstRender || _markdownChanged)
        {
            _markdownChanged = false;

            // create markup from markdown source
            HtmlContent = await MarkdownToMarkupStringAsync();
            StateHasChanged();

            // notify that content converted from markdown
            if (OnContentConverted.HasDelegate)
            {
                await OnContentConverted.InvokeAsync();
            }
            //await _jsModule.InvokeVoidAsync("highlight");
            //await _jsModule.InvokeVoidAsync("addCopyButton");
        }
    }

    /// <summary>
    /// Converts markdown, provided in Content or from markdown file stored as a static asset, to MarkupString for rendering.
    /// </summary>
    /// <returns>MarkupString</returns>
    private async Task<MarkupString> MarkdownToMarkupStringAsync()
    {
        return ConvertToMarkupString(Content);
    }
    private static MarkupString ConvertToMarkupString(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .DisableHtml()
                    .Build();

            // Convert markdown string to HTML, with raw HTML disabled to prevent XSS
            var html = Markdown.ToHtml(value, pipeline);

            return new MarkupString(html);
        }

        return new MarkupString();
    }
}
