# How to Contribute

If you are interested in contributing to CefSharp.Dom, Thank you!

If you want to contribute, ask before submitting a PR.
If you don’t know what to do, ASK!

## Getting setup

1. Clone this repository using your favorite tool

2. Create a self signed certificate:

The test server requires a self-signed certificate at this location: `.\lib\PuppeteerSharp.TestServer\testCert.cer`

You can run this PowerShell script on Windows

```
New-SelfSignedCertificate -Subject "localhost" -FriendlyName "Puppeteer" -CertStoreLocation "cert:\CurrentUser\My"

Get-ChildItem -Path cert:\CurrentUSer\my | where { $_.friendlyname -eq "Puppeteer" } | Export-Certificate -FilePath C:\Projects\CefSharp.Dom\lib\PuppeteerSharp.TestServer\testCert.cer

```

## Code reviews

All submissions, including submissions by project members, require review. We
use GitHub pull requests for this purpose. Consult
[GitHub Help](https://help.github.com/articles/about-pull-requests/) for more
information on pull requests.

## Code Style

Though this list will change over time, these are the things to consider now:
 * [We are team spaces](https://www.youtube.com/watch?v=SsoOG6ZeyUI).
 * Every public API should have an XML documentation.
 * Try to follow the current style.
 * Don’t reinvent the wheel.

## Commit Messages

Don’t worry about commit messages or about how many commits your PR has. [Your PR will be squashed](https://help.github.com/articles/about-pull-request-merges/#squash-and-merge-your-pull-request-commits), so the commit message will be set at that time.

## Writing Tests

* Every feature should be accompanied by a test.
* Every public api event/method should be accompanied by a test.
