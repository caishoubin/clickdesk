# Website AdSense setup

The public website is hosted at <https://clickdesk.still-moose-7651.chatgpt.site/>. The `website/` directory mirrors its published static files.

## Published verification configuration

- Publisher account: `pub-6637568737373692`.
- The homepage, privacy page, and contact page declare `google-adsense-account` metadata with `ca-pub-6637568737373692`.
- `/ads.txt` contains `google.com, pub-6637568737373692, DIRECT, f08c47fec0942fa0`.
- Privacy and contact pages are linked from the homepage.

This configuration prepares ownership verification. It does not request or display advertisements, and it does not confirm that Google has accepted or reviewed the site.

## Account steps

1. In AdSense, select **Add site** and enter `https://clickdesk.still-moose-7651.chatgpt.site/`.
2. If Google accepts this hosted address, choose the **Meta tag** verification method. The supplied publisher tag is already in the website's `<head>`.
3. Select **Verify**, then **Request review** when available. Google may use different wording in its localized interface.
4. Complete the required payments profile using the owner's actual details in AdSense.

The account dashboard has not been operated or submitted by this website deployment. If AdSense rejects the hosted address, follow its displayed domain requirements before proceeding; a custom domain and its hosting configuration may be needed. Ownership verification and policy approval remain Google's decisions.

## Before enabling advertisements

After approval, configure real ad units or Auto ads in the publisher account, add Google's account-specific ad-serving script, and update the privacy page to describe the active configuration. Configure the required consent flow, including a Google-certified consent management platform for users in the EEA, UK, and Switzerland as applicable. The verification-only release intentionally loads no ad-serving script.

Official references: [Add a new site](https://support.google.com/adsense/answer/12169212?hl=en), [ads.txt](https://support.google.com/adsense/answer/12171612?hl=en), and [consent management requirements](https://support.google.com/adsense/answer/13554116?hl=en).
