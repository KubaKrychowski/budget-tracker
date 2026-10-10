package com.wydatki.app.widget;

import java.io.IOException;

/** Sesja widzetu wygasla albo zostala odwolana — trzeba zalogowac sie ponownie (to nie jest blad sieci). */
final class AuthRequiredException extends IOException {
    AuthRequiredException(String message) {
        super(message);
    }
}
