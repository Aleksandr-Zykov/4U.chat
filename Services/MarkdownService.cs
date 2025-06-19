using Markdig;
using System.Text.RegularExpressions;
using System.Web;

namespace _4U.chat.Services;

public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;
    private readonly Dictionary<string, string> _latexPlaceholders = new();
    private readonly Dictionary<string, string> _codeBlockPlaceholders = new();

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public string ToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        // Clear placeholders for new conversion
        _latexPlaceholders.Clear();
        _codeBlockPlaceholders.Clear();

        // Preprocess LaTeX expressions first
        var latexPreprocessed = PreprocessLatex(markdown);
        
        // Preprocess the text to handle newlines properly for chat messages
        // Convert single newlines to HTML line breaks while preserving double newlines as paragraphs
        var processedMarkdown = PreprocessChatText(latexPreprocessed);
        
        var html = Markdown.ToHtml(processedMarkdown, _pipeline);
        
        // Post-process to add code block headers
        html = PostProcessCodeBlocks(html);
        
        // Restore LaTeX expressions with proper classes for KaTeX rendering
        html = PostProcessLatex(html);
        
        return html;
    }
    
    private string PreprocessChatText(string text)
    {
        // First, protect existing double newlines by replacing them with a placeholder
        var placeholder = "DOUBLE_NEWLINE_PLACEHOLDER";
        text = text.Replace("\n\n", placeholder);
        
        // Then replace single newlines with HTML line breaks
        text = text.Replace("\n", "  \n"); // Two spaces + newline = line break in Markdown
        
        // Restore double newlines (paragraph breaks)
        text = text.Replace(placeholder, "\n\n");
        
        return text;
    }
    
    private string PostProcessCodeBlocks(string html)
    {
        // Regular expression to match code blocks with language classes
        var codeBlockPattern = @"<pre><code class=""language-(\w+)"">(.*?)</code></pre>";
        var plainCodeBlockPattern = @"<pre><code>(.*?)</code></pre>";
        
        // First handle code blocks with language specified
        html = Regex.Replace(html, codeBlockPattern, match =>
        {
            var language = match.Groups[1].Value;
            var code = match.Groups[2].Value;
            var languageDisplay = GetLanguageDisplayName(language);
            
            return CreateCodeBlockWithHeader(code, language, languageDisplay);
        }, RegexOptions.Singleline);
        
        // Then handle plain code blocks without language
        html = Regex.Replace(html, plainCodeBlockPattern, match =>
        {
            var code = match.Groups[1].Value;
            
            // Skip if this was already processed (has our wrapper)
            if (code.Contains("code-block-header"))
                return match.Value;
                
            return CreateCodeBlockWithHeader(code, "text", "Text");
        }, RegexOptions.Singleline);
        
        return html;
    }
    
    private string CreateCodeBlockWithHeader(string code, string language, string languageDisplay)
    {
        var codeId = Guid.NewGuid().ToString("N")[..8]; // Short unique ID
        
        return $@"
<div class=""code-block-wrapper"" data-language=""{language}"" data-code-id=""{codeId}"">
    <div class=""code-block-header"">
        <div class=""code-language-label"">{languageDisplay}</div>
        <div class=""code-actions"">
            <button class=""code-action-btn download-btn"" onclick=""downloadCode('{codeId}', '{language}')"" title=""Download {languageDisplay} file"">
                <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"">
                    <path d=""M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4""/>
                    <polyline points=""7,10 12,15 17,10""/>
                    <line x1=""12"" y1=""15"" x2=""12"" y2=""3""/>
                </svg>
            </button>
            <button class=""code-action-btn copy-btn"" onclick=""copyCode('{codeId}')"" title=""Copy to clipboard"">
                <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"">
                    <rect x=""9"" y=""9"" width=""13"" height=""13"" rx=""2"" ry=""2""/>
                    <path d=""M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1""/>
                </svg>
            </button>
        </div>
    </div>
    <pre><code class=""language-{language}"" id=""code-{codeId}"">{code}</code></pre>
</div>";
    }
    
    private string GetLanguageDisplayName(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "js" or "javascript" => "JavaScript",
            "ts" or "typescript" => "TypeScript",
            "py" or "python" => "Python",
            "cs" or "csharp" or "c#" => "C#",
            "java" => "Java",
            "cpp" or "c++" => "C++",
            "c" => "C",
            "html" => "HTML",
            "css" => "CSS",
            "scss" or "sass" => "Sass",
            "json" => "JSON",
            "xml" => "XML",
            "yaml" or "yml" => "YAML",
            "md" or "markdown" => "Markdown",
            "sh" or "bash" => "Bash",
            "ps1" or "powershell" => "PowerShell",
            "sql" => "SQL",
            "php" => "PHP",
            "rb" or "ruby" => "Ruby",
            "go" => "Go",
            "rs" or "rust" => "Rust",
            "kt" or "kotlin" => "Kotlin",
            "swift" => "Swift",
            "dart" => "Dart",
            "r" => "R",
            "matlab" => "MATLAB",
            "tex" or "latex" => "LaTeX",
            "dockerfile" => "Dockerfile",
            "vue" => "Vue",
            "jsx" => "JSX",
            "tsx" => "TSX",
            _ => language.ToUpperInvariant()
        };
    }
    
    private string PreprocessLatex(string text)
    {
        // First protect inline code blocks and full code blocks from LaTeX processing
        var protectedText = ProtectCodeBlocks(text);
        
        // Process different LaTeX delimiters in order:
        // 1. Block LaTeX with $$...$$ 
        protectedText = ProcessBlockLatex(protectedText);
        
        // 2. Block LaTeX with \[...\] 
        protectedText = ProcessBracketBlockLatex(protectedText);
        
        // 3. Block LaTeX with [...] (for display math)
        protectedText = ProcessSquareBracketLatex(protectedText);
        
        // 4. Inline LaTeX with $...$
        protectedText = ProcessInlineLatex(protectedText);
        
        // Restore protected code blocks
        protectedText = RestoreCodeBlocks(protectedText);
        
        return protectedText;
    }
    
    private string ProcessBlockLatex(string text)
    {
        // Simpler block LaTeX pattern: $$...$$
        var blockLatexPattern = @"\$\$([^$]+)\$\$";
        
        return Regex.Replace(text, blockLatexPattern, match =>
        {
            var latexContent = match.Groups[1].Value.Trim();
            var placeholder = $"LATEX_BLOCK_{Guid.NewGuid():N}";
            _latexPlaceholders[placeholder] = latexContent;
            return placeholder;
        }, RegexOptions.Singleline);
    }
    
    private string ProcessBracketBlockLatex(string text)
    {
        // LaTeX display math pattern: \[...\]
        var bracketPattern = @"\\\[([^\]]+)\\\]";
        
        return Regex.Replace(text, bracketPattern, match =>
        {
            var latexContent = match.Groups[1].Value.Trim();
            var placeholder = $"LATEX_BLOCK_{Guid.NewGuid():N}";
            _latexPlaceholders[placeholder] = latexContent;
            return placeholder;
        }, RegexOptions.Singleline);
    }
    
    private string ProcessSquareBracketLatex(string text)
    {
        // Square bracket display math pattern: [latex expression]
        // Only match if it contains explicit LaTeX commands (\command) to avoid conflicts with markdown links
        // Exclude patterns that look like markdown links by using negative lookahead for ](...) 
        var squareBracketPattern = @"\[([^\]]*\\[a-zA-Z]+[^\]]*)\](?!\s*\()";
        
        return Regex.Replace(text, squareBracketPattern, match =>
        {
            var latexContent = match.Groups[1].Value.Trim();
            var placeholder = $"LATEX_BLOCK_{Guid.NewGuid():N}";
            _latexPlaceholders[placeholder] = latexContent;
            return placeholder;
        }, RegexOptions.Singleline);
    }
    
    private string ProcessInlineLatex(string text)
    {
        // First, protect already processed blocks by temporarily replacing them
        var protectedText = text;
        var blockPlaceholders = new Dictionary<string, string>();
        
        // Find and temporarily replace already processed blocks to avoid interference
        var blockPattern = @"LATEX_BLOCK_[a-zA-Z0-9]+";
        protectedText = Regex.Replace(protectedText, blockPattern, match =>
        {
            var placeholder = $"TEMP_BLOCK_{Guid.NewGuid():N}";
            blockPlaceholders[placeholder] = match.Value;
            return placeholder;
        });
        
        // Also protect $$ blocks
        var dollarsPattern = @"\$\$[^$]*\$\$";
        protectedText = Regex.Replace(protectedText, dollarsPattern, match =>
        {
            var placeholder = $"TEMP_DOLLARS_{Guid.NewGuid():N}";
            blockPlaceholders[placeholder] = match.Value;
            return placeholder;
        });
        
        // Now match simple inline LaTeX: $...$ 
        var inlineLatexPattern = @"\$([^$\n\r]+)\$";
        
        protectedText = Regex.Replace(protectedText, inlineLatexPattern, match =>
        {
            var latexContent = match.Groups[1].Value.Trim();
            var placeholder = $"LATEX_INLINE_{Guid.NewGuid():N}";
            _latexPlaceholders[placeholder] = latexContent;
            return placeholder;
        });
        
        // Restore the protected blocks
        foreach (var kvp in blockPlaceholders)
        {
            protectedText = protectedText.Replace(kvp.Key, kvp.Value);
        }
        
        return protectedText;
    }
    
    private string PostProcessLatex(string html)
    {
        foreach (var kvp in _latexPlaceholders)
        {
            var placeholder = kvp.Key;
            var latexContent = kvp.Value;
            
            if (placeholder.StartsWith("LATEX_BLOCK_"))
            {
                // Replace block LaTeX placeholders with div elements
                // Don't encode the display text, only the data attribute
                var blockHtml = $@"<div class=""katex-block"" data-latex=""{HttpUtility.HtmlEncode(latexContent)}"">$${latexContent}$$</div>";
                html = html.Replace(placeholder, blockHtml);
            }
            else if (placeholder.StartsWith("LATEX_INLINE_"))
            {
                // Replace inline LaTeX placeholders with span elements
                // Don't encode the display text, only the data attribute
                var inlineHtml = $@"<span class=""katex-inline"" data-latex=""{HttpUtility.HtmlEncode(latexContent)}"">${latexContent}$</span>";
                html = html.Replace(placeholder, inlineHtml);
            }
        }
        
        return html;
    }
    
    private string ProtectCodeBlocks(string text)
    {
        // First protect full code blocks (triple backticks)
        var codeBlockPattern = @"```(?:[^`]|`(?!``))*```";
        text = Regex.Replace(text, codeBlockPattern, match =>
        {
            var placeholder = $"CODE_BLOCK_{Guid.NewGuid():N}";
            _codeBlockPlaceholders[placeholder] = match.Value;
            return placeholder;
        }, RegexOptions.Singleline);
        
        // Then protect inline code blocks (single backticks)
        var inlineCodePattern = @"`([^`\n\r]+)`";
        text = Regex.Replace(text, inlineCodePattern, match =>
        {
            var placeholder = $"INLINE_CODE_{Guid.NewGuid():N}";
            _codeBlockPlaceholders[placeholder] = match.Value;
            return placeholder;
        });
        
        return text;
    }
    
    private string RestoreCodeBlocks(string text)
    {
        foreach (var kvp in _codeBlockPlaceholders)
        {
            text = text.Replace(kvp.Key, kvp.Value);
        }
        return text;
    }
}