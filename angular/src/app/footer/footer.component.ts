import { Component } from '@angular/core';

@Component({
  selector: 'abp-footer',
  template: `
    <div class="lpx-footbar-container end-0">
      <div class="lpx-footbar">
        <div class="lpx-footbar-copyright">
          <span>{{ currentYear }}© </span>
          <a href="https://dignite.com/" target="_blank">Powered by Dignite</a>
        </div>
        <div class="lpx-footbar-solo-links">
          <a href="#">About</a>
          <a href="#">Privacy</a>
          <a href="#">Contact</a>
        </div>
      </div>
    </div>
  `,
})
export class FooterComponent {
  currentYear = new Date().getFullYear();
}
