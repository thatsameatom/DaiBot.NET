using Discord;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public static class AutoLegalityExtensionsDiscord
{
    public static async Task ReplyWithLegalizedSetAsync(this ISocketMessageChannel channel, ITrainerInfo sav, ShowdownSet set)
    {
        if (set.Species <= 0)
        {
            await channel.SendMessageAsync("<a:warning:1206483664939126795> Oops! No he podido interpretar tu mensaje. Si pretendías convertir algo, ¡por favor, vuelve a comprobar lo que estás pegando!").ConfigureAwait(false);
            return;
        }

        try
        {
            var template = AutoLegalityWrapper.GetTemplate(set);
            var pkm = sav.GetLegal(template, out var result);
            var la = new LegalityAnalysis(pkm);
            var spec = GameInfo.Strings.Species[template.Species];
            if (!la.Valid)
            {
                var reason = result == "Timeout" ? $"Este **{spec}** tomó demasiado tiempo para generarse." : result == "VersionMismatch" ? "Solicitud denegada: Las versiones de **PKHeX** y **Auto-Legality Mod** no coinciden." : $"No se puede crear un **{spec}** con esos datos.";
                var imsg = $"<a:no:1206485104424128593> Oops! {reason}";
                if (result == "Failed")
                    imsg += $"\n{AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm)}";
                // Create an embed
                var embed1 = new EmbedBuilder()
                    .WithDescription(imsg)
                    .WithColor(Color.Red)
                    .WithAuthor(new EmbedAuthorBuilder
                    {
                        Name = "Error de legalidad",
                        IconUrl = "https://img.freepik.com/free-icon/warning_318-478601.jpg" // Replace with the URL of the author's icon
                    })
                    .WithImageUrl("https://i.imgur.com/Y64hLzW.gif") // Replace with the URL of an image related to the error
                    .WithThumbnailUrl("https://i.imgur.com/DWLEXyu.png")  // Replace with the URL of an image related to the error
                    .Build();

                await channel.SendMessageAsync(embed: embed1).ConfigureAwait(false);
                return;
            }


            var speciesName = GameInfo.Strings.Species[template.Species];
            var successMsg = $" Aqui esta tu **{speciesName}** legalizado.";
            var showdownText = ReusableActions.GetFormattedShowdownText(pkm);
            bool canGmax = pkm is PK8 pk8 && pk8.CanGigantamax;
            var speciesImageUrl = AbstractTrade<PK9>.PokeImg(pkm, canGmax, false);

            var embed = new EmbedBuilder()
                .WithDescription(successMsg)
                .WithColor(Color.Green)
                .WithAuthor(new EmbedAuthorBuilder
                {
                    Name = "Legalización Exitosa",
                    IconUrl = "https://www.opvakantienaar.com/wp-content/themes/yootheme/cache/Yes-Sign-c182f662.png" // Replace with the URL of the success icon
                })
                .WithThumbnailUrl(speciesImageUrl) // Use the Pokémon image URL from pokeImgUrl
                .AddField("__**Especie**__", spec, true)
                .AddField("__**Tipo de encuentro**__", la.EncounterOriginal.Name, true)
                .AddField("__**Resultado**__", result, true)
                .AddField("__**Detalles**__:", showdownText)
                .Build();

            await channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
            await channel.SendPKMAsync(pkm).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(AutoLegalityExtensionsDiscord));
            var errorMessage = $"<a:no:1206485104424128593> Oops! Ocurrió un problema inesperado con este Showdown Set:\n```{string.Join("\n", set.GetSetLines())}```";

            var embedError = new EmbedBuilder()
                .WithDescription(errorMessage)
                .WithColor(Color.Red)
                .WithAuthor(new EmbedAuthorBuilder
                {
                    Name = "Error de Legalidad",
                    IconUrl = "https://img.freepik.com/free-icon/warning_318-478601.jpg" // Replace with the URL of the author's icon
                })
                .WithThumbnailUrl("https://i.imgur.com/DWLEXyu.png")
                .Build();

            await channel.SendMessageAsync(embed: embedError).ConfigureAwait(false);
        }
    }

    public static Task ReplyWithLegalizedSetAsync(this ISocketMessageChannel channel, string content, byte gen)
    {
        content = ReusableActions.StripCodeBlock(content);
        var set = new ShowdownSet(content);
        var sav = AutoLegalityWrapper.GetTrainerInfo(gen);
        return channel.ReplyWithLegalizedSetAsync(sav, set);
    }

    public static Task ReplyWithLegalizedSetAsync<T>(this ISocketMessageChannel channel, string content) where T : PKM, new()
    {
        content = ReusableActions.StripCodeBlock(content);
        var set = new ShowdownSet(content);
        var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
        return channel.ReplyWithLegalizedSetAsync(sav, set);
    }

    public static async Task ReplyWithLegalizedSetAsync(this ISocketMessageChannel channel, IAttachment att)
    {
        var download = await NetUtil.DownloadPKMAsync(att).ConfigureAwait(false);
        if (!download.Success)
        {
            await channel.SendMessageAsync(download.ErrorMessage).ConfigureAwait(false);
            return;
        }

        var pkm = download.Data!;
        string pokeImgUrl = "https://i.imgur.com/MlkpDow.gif"; // Replace with a suitable default image URL

        if (pkm is PK8 pk8)
        {
            pokeImgUrl = AbstractTrade<PK8>.PokeImg(pk8, false, false);
        }
        else if (pkm is PK9 pk9)
        {
            pokeImgUrl = AbstractTrade<PK9>.PokeImg(pk9, false, false);
        }
        else if (pkm is PB8 pb8)
        {
            pokeImgUrl = AbstractTrade<PB8>.PokeImg(pb8, false, false);
        }
        else if (pkm is PA8 pa8)
        {
            pokeImgUrl = AbstractTrade<PB8>.PokeImg(pa8, false, false);
        }

        if (pokeImgUrl == null)
        {
            // Handle the case where the type is not recognized
            await channel.SendMessageAsync("Tipo de Pokémon no reconocido para obtener la imagen.").ConfigureAwait(false);
            return;
        }
        var legalityAnalysis = new LegalityAnalysis(pkm);
        if (legalityAnalysis.Valid)
        {
            var embedLegal = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithAuthor(new EmbedAuthorBuilder
                {
                    Name = "Advertencia!",
                    IconUrl = "https://img.freepik.com/free-icon/warning_318-478601.jpg" // Replace with the URL of the success icon
                })
                .WithThumbnailUrl("https://i.imgur.com/DWLEXyu.png")
                .WithImageUrl(pokeImgUrl)
                .AddField("__**Estado**__", "Fallo al Legalizar.", true)
                .AddField("__**Razón**__", "Ya es legal.", true)
                .AddField("__**Información**__:", $"El archivo **{download.SanitizedFileName}** ya es legal.")
                .Build();

            await channel.SendMessageAsync(embed: embedLegal).ConfigureAwait(false);
            return;
        }

        var legal = pkm.LegalizePokemon();
        var legalityAnalysisLegal = new LegalityAnalysis(legal);
        if (!legalityAnalysisLegal.Valid)
        {
            var embedNotLegal = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithAuthor(new EmbedAuthorBuilder
                {
                    Name = "Error de Legalización",
                    IconUrl = "https://img.freepik.com/free-icon/warning_318-478601.jpg" // Replace with the URL of the author's icon
                })
                .WithThumbnailUrl("https://i.imgur.com/DWLEXyu.png")
                .WithImageUrl(pokeImgUrl)
                .AddField("__**Estado**__", "Fallo al Legalizar.", true)
                .AddField("__**Razón**__", "No puede ser legalizado.", true)
                .AddField("__**Información**__:", $"El archivo **{download.SanitizedFileName}** no se puede legalizar.")
                .Build();

            await channel.SendMessageAsync(embed: embedNotLegal).ConfigureAwait(false);
            return;
        }

        legal.RefreshChecksum();

        var embed = new EmbedBuilder()
        .WithColor(Color.Green)
        .WithAuthor(new EmbedAuthorBuilder
        {
            Name = "Legalización Exitosa",
            IconUrl = "https://www.opvakantienaar.com/wp-content/themes/yootheme/cache/Yes-Sign-c182f662.png" // Replace with the URL of the success icon
        })
        .WithThumbnailUrl(pokeImgUrl) // Use the Pokémon image URL from pokeImgUrl
        .AddField("__**Estado**__", "Legalización exitosa.", true)
        .AddField("__**Información**__:", $"Aquí está su PKM legalizado: **{download.SanitizedFileName}**!")
        .AddField("__**Showdown Text**__:", ReusableActions.GetFormattedShowdownText(legal))
        .Build();

        await channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
        await channel.SendPKMAsync(pkm).ConfigureAwait(false);
    }
}
