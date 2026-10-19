# Website AdSense setup

The free GitHub Pages address for ClickDesk is <https://caishoubin.github.io/clickdesk/>. Its download buttons point to the existing [GitHub releases](https://github.com/caishoubin/clickdesk/releases/latest).

The original ChatGPT Sites website remains available at <https://clickdesk.still-moose-7651.chatgpt.site/>. AdSense rejected that nested address with a domain validation error, so it should not be used in the Add site form.

## Published verification configuration

- Publisher account: `pub-6637568737373692`.
- The GitHub Pages root homepage and ClickDesk pages declare `google-adsense-account` metadata with `ca-pub-6637568737373692`.
- Root verification file: <https://caishoubin.github.io/ads.txt>.
- The file contains `google.com, pub-6637568737373692, DIRECT, f08c47fec0942fa0`.
- Privacy and contact pages are linked from the ClickDesk homepage. The mirror's privacy notice describes GitHub Pages hosting.
- The existing blog and its articles remain in the Pages repository; its homepage includes a ClickDesk project link.

The owner's AdSense dashboard screenshot on October 19, 2026 confirms that ownership verification passed and a review was requested. The displayed status is Getting ready. Approval is pending. The website configuration does not request or display advertisements.

## Account steps

1. In AdSense, select **Add site** and enter **`caishoubin.github.io`**. Enter the site domain without `/clickdesk/`.
2. Select the **ads.txt** verification method. The authorized-seller file is at the domain root. Alternatively, the root homepage also contains the publisher meta tag.
3. Select **Verify**, then **Request review** when available. Google may use different wording in its localized interface.
4. Keep using the existing publisher account. Do not create a second AdSense account for the new address.

Google allows sites under domains on the Public Suffix List; `github.io` appears in that list. The owner has added this domain and submitted it for review. Policy approval remains Google's decision. Review may include the existing blog under the same domain. The dashboard steps above document the completed setup; they do not need to be repeated while review is pending.

## Maintain the free mirror

The editable ClickDesk source remains in `website/`. To adapt its links and hosting privacy notice for GitHub Pages while preserving the existing blog:

```sh
python3 scripts/prepare-github-pages.py /absolute/path/to/caishoubin.github.io
```

Review and push the generated changes in the `caishoubin/caishoubin.github.io` repository. The script publishes ClickDesk files under `/clickdesk/`, adds the root verification metadata and seller entry, and adds a project link to the existing blog. It does not enable ad requests. GitHub Pages public-repository hosting uses the GitHub Free allowance; no domain registration is required for the provided `github.io` address.

## Before enabling advertisements

After approval, configure real ad units or Auto ads in the publisher account, add Google's account-specific ad-serving script, and update the privacy page to describe the active configuration. Configure the required consent flow, including a Google-certified consent management platform for users in the EEA, UK, and Switzerland as applicable. The verification-only release intentionally loads no ad-serving script.

Official references: [Allowed sites](https://support.google.com/adsense/answer/12170421?hl=en), [Add a new site](https://support.google.com/adsense/answer/12169212?hl=en), [Public Suffix List](https://publicsuffix.org/list/public_suffix_list.dat), [GitHub Pages](https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages), and [consent management requirements](https://support.google.com/adsense/answer/13554116?hl=en).
