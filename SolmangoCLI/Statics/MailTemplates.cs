using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace SolmangoCLI.Statics;

public static class MailTemplates
{
    private static AlternateView ComposeBody(Content content)
    {
        var linkedResources = new List<LinkedResource>();
        var htmlBuilder = new StringBuilder();
        foreach (var section in content.Sections)
        {
            switch (section.type)
            {
                case Content.Type.Text:
                    htmlBuilder.Append(section.content);
                    break;

                case Content.Type.Asset:
                    var res = new LinkedResource(section.content, MediaTypeNames.Image.Jpeg)
                    {
                        ContentId = Guid.NewGuid().ToString()
                    };
                    htmlBuilder.Append($@"<img src='cid:{res.ContentId}'/>");
                    linkedResources.Add(res);
                    break;
            }
        }
        var alternateView = AlternateView.CreateAlternateViewFromString(htmlBuilder.ToString(), null, MediaTypeNames.Text.Html);
        linkedResources.ForEach(l => alternateView.LinkedResources.Add(l));
        return alternateView;
    }

    public class Content
    {
        public enum Type
        { Asset, Text }

        public List<(Type type, string content)> Sections { get; private set; }

        private Content()
        {
            Sections = new List<(Type, string)>();
        }

        public static Builder Compose() => new Builder();

        public class Builder
        {
            private readonly Content content;

            public Builder()
            {
                content = new Content();
            }

            public static implicit operator Content(Builder builder) => builder.Build();

            public Builder EmbedImage(string path)
            {
                content.Sections.Add((Type.Asset, path));
                return this;
            }

            public Builder Text(string htmlBody)
            {
                content.Sections.Add((Type.Text, htmlBody));
                return this;
            }

            public Content Build() => content;
        }
    }
}