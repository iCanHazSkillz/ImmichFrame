[![Contributors][contributors-shield]][contributors-url]
[![Forks][forks-shield]][forks-url]
[![Stargazers][stars-shield]][stars-url]
[![Issues][issues-shield]][issues-url]
[![MIT License][license-shield]][license-url]

<!-- PROJECT LOGO -->
<br />
<div align="center">
  <a href="https://github.com/3rob3/ImmichFrame">
    <img src="design/AppIcon.png" alt="Logo" width="200" height="200">
  </a>

  <h3 align="center">ImmichFrame</h3>

  <p align="center">
    An awesome way to display your photos as a digital photo frame
    <br />
    <a href="https://immich.app/"><strong>Explore immich »</strong></a>
    <br />
    <br />
    <a href="https://immichframe.dev">Documentation</a>
    ·
    <a href="https://demo.immichframe.dev">Demo</a>
    ·
    <a href="https://github.com/3rob3/ImmichFrame/issues">Report Bug</a>
    ·
    <a href="https://github.com/3rob3/ImmichFrame/issues">Request Feature</a>
  </p>
</div>

## 🔀 Fork Notes

This repository is a fork of the original [3rob3/ImmichFrame](https://github.com/3rob3/ImmichFrame) project and keeps building on that foundation.

If you are coming from the original repo, the biggest differences in this fork are:

- A browser-based `/admin` page for admin sign-in
- A browser-based `/admin/settings` page for runtime configuration
- Optional admin credentials for protecting access to the admin UI
- Runtime-managed settings and custom CSS stored in `App_Data`

Most of the frame experience is still based on the original ImmichFrame project, but this fork adds a more convenient web-admin workflow so common changes no longer require editing bootstrap config files by hand.

## 🔐 Admin Access In This Fork

This fork supports an optional admin login for the `/admin` and `/admin/settings` pages.

- If `IMMICHFRAME_AUTH_BASIC_ADMIN_USER` and `IMMICHFRAME_AUTH_BASIC_ADMIN_HASH` are not set, the admin login page is disabled.
- If they are set, the admin UI is protected and available at `/admin`.

When using Docker, generate the password hash locally with `htpasswd`, for example:

```bash
htpasswd -nbB admin your-password
```

When placing that hash in a Docker `.env` file used by `docker compose`, escape every `$` as `$$`.

Example:

```env
IMMICHFRAME_AUTH_BASIC_ADMIN_USER=admin
IMMICHFRAME_AUTH_BASIC_ADMIN_HASH=$$2y$$05$$...
```

This escaping is required so Docker Compose does not try to interpret parts of the hash as environment variables.

## ⚠️ Upgrade Note For Users Coming From The Original Repo

If you previously ran the original repo in the same browser and then switch to this fork, clear the browser cache before testing.

The frontend bundle, cached assets, and stored browser state from the original repo can cause stale UI behavior until the cache is cleared. This is especially important if:

- `/admin` or `/admin/settings` do not behave as expected
- widget/settings changes do not seem to apply
- the browser appears to load an older frontend after upgrading

## 📄 Documentation
You can find the documentation [here](https://immichframe.dev).

## 🖼️ Demo

You can find a working demo [here](https://demo.immichframe.dev).

<img src="/design/demo/web_demo.png" alt="Web Demo">

## ⚠️ Disclaimer

**This project is not affiliated with [immich][immich-github-url]!**

## 📜 License

[GNU General Public License v3.0](LICENSE.txt)

## 🆘 Help

[Discord Channel][support-url]

## 🙏 Acknowledgments

- BIG thanks to the [immich team][immich-github-url] for creating an awesome tool

## 🌟 Star History

[![Star History Chart](https://api.star-history.com/svg?repos=immichframe/immichframe&type=Date)](https://www.star-history.com/#immichframe/immichframe&Date)

<!-- MARKDOWN LINKS & IMAGES -->
<!-- https://www.markdownguide.org/basic-syntax/#reference-style-links -->

[contributors-shield]: https://img.shields.io/github/contributors/immichFrame/ImmichFrame.svg?style=for-the-badge
[contributors-url]: https://github.com/immichFrame/ImmichFrame/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/immichFrame/ImmichFrame.svg?style=for-the-badge
[forks-url]: https://github.com/immichFrame/ImmichFrame/network/members
[stars-shield]: https://img.shields.io/github/stars/immichFrame/ImmichFrame.svg?style=for-the-badge
[stars-url]: https://github.com/immichFrame/ImmichFrame/stargazers
[issues-shield]: https://img.shields.io/github/issues/immichFrame/ImmichFrame.svg?style=for-the-badge
[issues-url]: https://github.com/immichFrame/ImmichFrame/issues
[license-shield]: https://img.shields.io/github/license/immichFrame/ImmichFrame.svg?style=for-the-badge
[license-url]: https://github.com/immichFrame/ImmichFrame/blob/master/LICENSE.txt
[releases-url]: https://github.com/immichFrame/ImmichFrame/releases/latest
[support-url]: https://discord.com/channels/979116623879368755/1217843270244372480
[openweathermap-url]: https://openweathermap.org/
[immich-github-url]: https://github.com/immich-app/immich
[immich-api-url]: https://immich.app/docs/features/command-line-interface#obtain-the-api-key
