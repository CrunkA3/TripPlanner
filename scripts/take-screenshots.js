'use strict';

/**
 * TripPlanner – screenshot script
 *
 * Starts a headless Chromium browser, registers and logs in a test user,
 * creates a wishlist with a sample place and a trip, then captures full-page
 * screenshots of every main application page.
 *
 * Environment variables:
 *   APP_URL          – base URL of the running application (default: http://localhost:8980)
 *   SCREENSHOTS_DIR  – absolute path for output PNG files
 *                      (default: ../assets/screenshots relative to this script)
 */

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const BASE_URL = (process.env.APP_URL || 'http://localhost:8980').replace(/\/$/, '');
const SCREENSHOTS_DIR = process.env.SCREENSHOTS_DIR
    ? process.env.SCREENSHOTS_DIR
    : path.join(__dirname, '..', 'assets', 'screenshots');

const TEST_EMAIL = 'screenshots@tripplanner.test';
const TEST_PASSWORD = 'Screenshots1!';

// ── helpers ───────────────────────────────────────────────────────────────────

function ensureDir(dir) {
    fs.mkdirSync(dir, { recursive: true });
}

async function screenshot(page, filename, description) {
    const filepath = path.join(SCREENSHOTS_DIR, filename);
    await page.screenshot({ path: filepath, fullPage: true });
    console.log(`  ✓ ${filename}  (${description})`);
}

/**
 * Clicks a fluent-text-field (or any element with a label attribute equal to
 * `label`) to give it focus, then types the value via keyboard and presses Tab
 * to trigger the Blazor onchange binding.
 */
async function fillByLabel(page, label, value) {
    // Covers fluent-text-field and fluent-text-area with a [label] attribute
    const sel = `fluent-text-field[label="${label}"], fluent-text-area[label="${label}"]`;
    const locator = page.locator(sel).first();
    await locator.click();
    await page.keyboard.press('Control+a');
    await page.keyboard.type(value, { delay: 20 });
    await page.keyboard.press('Tab');
    await page.waitForTimeout(300);
}

/**
 * Sets the value of a name-bound form field (used in static-SSR forms such as
 * Account/Register and Account/Login) via JavaScript so the server-side POST
 * picks it up.
 *
 * Fluent UI components (e.g. fluent-text-field) are custom web elements whose
 * visible host element is NOT an HTMLInputElement.  The actual <input> lives
 * inside the component's shadow DOM.  We pierce the shadow root so that the
 * native HTMLInputElement setter is always called on a real input element,
 * preventing the "Illegal invocation" TypeError.
 */
async function setFormFieldByName(page, name, value) {
    await page.evaluate(({ n, v }) => {
        const host = document.querySelector(`[name="${n}"]`);
        if (!host) return;
        // Resolve the real input: for web components the native <input> is in the
        // shadow DOM; for plain HTML inputs the host element itself is the input.
        const el = (host.shadowRoot && host.shadowRoot.querySelector('input')) || host;
        const nativeDescriptor = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value');
        if (nativeDescriptor && nativeDescriptor.set && el instanceof window.HTMLInputElement) {
            nativeDescriptor.set.call(el, v);
        } else {
            el.value = v;
        }
        el.dispatchEvent(new Event('input', { bubbles: true }));
        el.dispatchEvent(new Event('change', { bubbles: true }));
    }, { n: name, v: value });
}

// ── main ──────────────────────────────────────────────────────────────────────

async function main() {
    ensureDir(SCREENSHOTS_DIR);

    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const page = await context.newPage();

    try {
        // ── 1. Home page (public) ──────────────────────────────────────────────
        console.log('\n[1] Home page (public)');
        await page.goto(BASE_URL, { waitUntil: 'domcontentloaded' });
        await page.waitForTimeout(1500);
        await screenshot(page, '01-home.png', 'Home page – public');

        // ── 2. Register a test user ────────────────────────────────────────────
        console.log('\n[2] Register test user');
        await page.goto(`${BASE_URL}/Account/Register`, { waitUntil: 'domcontentloaded' });
        await page.waitForTimeout(1500);

        await setFormFieldByName(page, 'Input.Email', TEST_EMAIL);
        await setFormFieldByName(page, 'Input.Password', TEST_PASSWORD);
        await setFormFieldByName(page, 'Input.ConfirmPassword', TEST_PASSWORD);

        // Accept privacy policy – fluent-checkbox keeps its native <input> in shadow DOM
        await page.evaluate(() => {
            const host = document.querySelector('[name="Input.AcceptPrivacyPolicy"]');
            if (!host) return;
            const cb = (host.shadowRoot && host.shadowRoot.querySelector('input[type="checkbox"]')) || host;
            cb.checked = true;
            cb.dispatchEvent(new Event('input', { bubbles: true }));
            cb.dispatchEvent(new Event('change', { bubbles: true }));
        });

        // Submit via the button click (triggers browser validation + Blazor anti-forgery)
        await Promise.all([
            page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }),
            page.locator('fluent-button[type="submit"]').click(),
        ]);

        // ── 3. Confirm e-mail (development flow shows the link on-screen) ──────
        console.log('\n[3] Confirm e-mail');
        await page.waitForTimeout(1000);
        const confirmLink = page.locator('a[href*="ConfirmEmail"]').first();
        if (await confirmLink.count() > 0) {
            await Promise.all([
                page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }),
                confirmLink.click(),
            ]);
            console.log('  ✓ Account confirmed');
        }

        // ── 4. Log in ──────────────────────────────────────────────────────────
        console.log('\n[4] Log in');
        await page.goto(`${BASE_URL}/Account/Login`, { waitUntil: 'domcontentloaded' });
        await page.waitForTimeout(1500);

        await setFormFieldByName(page, 'Input.Email', TEST_EMAIL);
        await setFormFieldByName(page, 'Input.Password', TEST_PASSWORD);

        // Submit via button click
        await Promise.all([
            page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }),
            page.locator('fluent-button[type="submit"]').click(),
        ]);
        await page.waitForTimeout(2000);
        console.log(`  ✓ Logged in – current URL: ${page.url()}`);

        // ── 5. Home page (authenticated) ──────────────────────────────────────
        console.log('\n[5] Home page (authenticated)');
        await page.goto(BASE_URL, { waitUntil: 'domcontentloaded' });
        await page.waitForTimeout(1500);
        await screenshot(page, '02-home-authenticated.png', 'Home page – authenticated');

        // ── 6. Wishlists page (empty) ──────────────────────────────────────────
        console.log('\n[6] Wishlists page');
        await page.goto(`${BASE_URL}/wishlists`, { waitUntil: 'domcontentloaded' });
        await page.waitForTimeout(1500);
        await screenshot(page, '03-wishlists-empty.png', 'Wishlists page – empty');

        // ── 7. Create a wishlist ───────────────────────────────────────────────
        console.log('\n[7] Create wishlist');
        await page.locator('fluent-button:has-text("Create Wishlist")').click();
        await page.waitForTimeout(1500);

        await fillByLabel(page, 'Name', 'Europe Dream Trip');
        await fillByLabel(page, 'Description', 'Places I want to visit across Europe');

        // Submit the create-wishlist form
        await page.locator('fluent-button[type="submit"]:has-text("Create")').click();
        await page.waitForTimeout(2500);
        await screenshot(page, '04-wishlists-with-data.png', 'Wishlists page – with wishlist');

        // ── 8. Wishlist detail page + add places ──────────────────────────────
        console.log('\n[8] Wishlist detail – add places');
        await page.locator('fluent-button:has-text("View")').first().click();
        await page.waitForTimeout(2500);
        await screenshot(page, '05-wishlist-detail-empty.png', 'Wishlist detail – empty');

        // Add first place
        await page.locator('fluent-button:has-text("Add Place")').click();
        await page.waitForTimeout(2000);

        await fillByLabel(page, 'Name', 'Eiffel Tower');
        await fillByLabel(page, 'Description', 'Iconic iron lattice tower on the Champ de Mars in Paris');

        await page.locator('fluent-button:has-text("Save")').first().click();
        await page.waitForTimeout(2500);

        // Add second place
        await page.locator('fluent-button:has-text("Add Place")').click();
        await page.waitForTimeout(2000);

        await fillByLabel(page, 'Name', 'Colosseum');
        await fillByLabel(page, 'Description', 'Ancient amphitheatre in the centre of Rome');

        await page.locator('fluent-button:has-text("Save")').first().click();
        await page.waitForTimeout(2500);

        await screenshot(page, '06-wishlist-detail-with-places.png', 'Wishlist detail – with places');

        // ── 9. Trips page (empty) ──────────────────────────────────────────────
        console.log('\n[9] Trips page');
        await page.goto(`${BASE_URL}/trips`, { waitUntil: 'domcontentloaded' });
        await page.waitForTimeout(1500);
        await screenshot(page, '07-trips-empty.png', 'Trips page – empty');

        // ── 10. Create a trip ─────────────────────────────────────────────────
        console.log('\n[10] Create trip');
        await page.locator('fluent-button:has-text("Create Trip")').click();
        await page.waitForTimeout(1500);

        await fillByLabel(page, 'Trip Name', 'Paris 2025');
        await fillByLabel(page, 'Description', 'A week exploring the best of Paris');
        await fillByLabel(page, 'Start Date (YYYY-MM-DD)', '2025-06-01');
        await fillByLabel(page, 'End Date (YYYY-MM-DD)', '2025-06-07');

        await page.locator('fluent-button:has-text("Save")').first().click();
        await page.waitForTimeout(2500);
        await screenshot(page, '08-trips-with-data.png', 'Trips page – with trip');

        // ── 11. Trip plan page ─────────────────────────────────────────────────
        console.log('\n[11] Trip plan page');
        await page.locator('fluent-button:has-text("Plan")').first().click();
        await page.waitForTimeout(3000);
        await screenshot(page, '09-trip-plan.png', 'Trip plan page');

        // ── 12. Map page ───────────────────────────────────────────────────────
        console.log('\n[12] Map page');
        await page.goto(`${BASE_URL}/map`, { waitUntil: 'domcontentloaded' });
        await page.waitForTimeout(2000);
        await screenshot(page, '10-map.png', 'Map page');

        console.log('\n✓ All screenshots saved to:', SCREENSHOTS_DIR);
    } catch (err) {
        console.error('\n✗ Screenshot script failed:', err.message);
        // Save a debug screenshot of the current state
        try {
            const errorPath = path.join(SCREENSHOTS_DIR, 'error-state.png');
            await page.screenshot({ path: errorPath, fullPage: true });
            console.error('  Debug screenshot saved to:', errorPath);
        } catch (_) { /* ignore secondary errors */ }
        throw err;
    } finally {
        await browser.close();
    }
}

main().catch((err) => {
    console.error(err);
    process.exit(1);
});
