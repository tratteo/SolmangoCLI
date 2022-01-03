using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace SolmangoCLI.Statics;

public static class MailTemplates
{
    public static MailMessage Dividend(string email, ShareHolder holder, string solAmount)
    {
        MailMessage mail = new MailMessage()
        {
            IsBodyHtml = true
        };
        mail.From = new MailAddress(email);
        mail.Subject = "Dividends distributions";
        mail.To.Add(holder.Email);
        mail.AlternateViews.Add(ComposeBody(
            Content.Compose()
            .Text(
                @$"Cheers {holder.Name}!<br>
                We want to thank you again for having invested with us!<br><br>
                Dividends have been distributed, and we are proud to communicate that
                <h2 style='color:#ed6cff';>{solAmount} SOL</h2>
                has been sent to your wallet <b>{holder.Address}</b><br>")
            .EmbedImage("res/assets/neonCloudsLogo512.png")));
        return mail;
    }

    private static AlternateView ComposeBody(Content content)
    {
        List<LinkedResource> linkedResources = new List<LinkedResource>();
        StringBuilder htmlBuilder = new StringBuilder();
        foreach (var section in content.Sections)
        {
            switch (section.type)
            {
                case Content.Type.Text:
                    htmlBuilder.Append(section.content);
                    break;

                case Content.Type.Asset:
                    LinkedResource res = new LinkedResource(section.content, MediaTypeNames.Image.Jpeg)
                    {
                        ContentId = Guid.NewGuid().ToString()
                    };
                    htmlBuilder.Append($@"<img src='cid:{res.ContentId}'/>");
                    linkedResources.Add(res);
                    break;
            }
        }
        AlternateView alternateView = AlternateView.CreateAlternateViewFromString(htmlBuilder.ToString(), null, MediaTypeNames.Text.Html);
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