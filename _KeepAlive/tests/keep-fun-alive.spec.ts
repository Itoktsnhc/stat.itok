import { test, expect } from '@playwright/test';

test('visit site', async ({ page }) => {
  await page.goto('http://stat.itok.xyz/');

  await expect(page).toHaveTitle(/stat.itok/);
});
