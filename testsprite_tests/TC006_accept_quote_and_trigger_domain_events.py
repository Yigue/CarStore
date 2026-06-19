import requests
import time

BASE_URL = "http://localhost:8080"
LOGIN_URL = f"{BASE_URL}/api/v1/users/login"
LEADS_URL = f"{BASE_URL}/api/v1/leads"
CARS_URL = f"{BASE_URL}/api/v1/cars"
QUOTES_URL = f"{BASE_URL}/api/v1/quotes"
CLIENTS_URL = f"{BASE_URL}/api/v1/clients"
TIMEOUT = 30

def test_acceptquoteandtriggerdomainevents():
    session = requests.Session()
    
    # 1. Login
    login_payload = {'email': 'admin@carstore.com', 'password': 'Admin123!'}
    login_resp = session.post(LOGIN_URL, json=login_payload, timeout=TIMEOUT)
    assert login_resp.status_code == 200, f"Login failed: {login_resp.text}"
    token = login_resp.json().get("token")
    assert token, "Token not found"
    
    headers = {
        'Authorization': f'Bearer {token}',
        'Content-Type': 'application/json'
    }
    
    # 2. Get valid car ID
    cars_resp = session.get(CARS_URL, headers=headers, timeout=TIMEOUT)
    assert cars_resp.status_code == 200, f"Get cars failed: {cars_resp.text}"
    cars_data = cars_resp.json()
    items = cars_data.get("items", [])
    assert len(items) > 0, "No cars found in database"
    car_id = items[0]["id"]
    
    # 3. Create lead
    lead_payload = {
        'clientName': 'John Doe',
        'email': 'johndoe@example.com',
        'phone': '1234567890',
        'source': 0, # Web
        'notes': 'Test notes',
        'interestedVehicleId': car_id
    }
    create_resp = session.post(LEADS_URL, headers=headers, json=lead_payload, timeout=TIMEOUT)
    assert create_resp.status_code == 201, f"Create lead failed: {create_resp.text}"
    lead_id = create_resp.json().get("id")
    assert lead_id, "Lead ID not found in response"
    
    # 4. Update status to Contactado (1)
    status_payload = {
        'newStatus': 1,
        'notes': 'Contacted client'
    }
    status_resp = session.patch(f"{LEADS_URL}/{lead_id}/status", headers=headers, json=status_payload, timeout=TIMEOUT)
    assert status_resp.status_code == 204, f"Update status failed: {status_resp.text}"
    
    # 5. Link vehicle (PATCH leads/{id}/vehicle)
    link_payload = {
        'vehicleId': car_id
    }
    link_resp = session.patch(f"{LEADS_URL}/{lead_id}/vehicle", headers=headers, json=link_payload, timeout=TIMEOUT)
    assert link_resp.status_code == 204, f"Link vehicle failed: {link_resp.text}"
    
    # 6. Convert lead to client
    convert_payload = {
        'dni': '12345678',
        'address': '123 Main St'
    }
    convert_resp = session.post(f"{LEADS_URL}/{lead_id}/convert", headers=headers, json=convert_payload, timeout=TIMEOUT)
    assert convert_resp.status_code in (200, 201), f"Convert lead failed: {convert_resp.text}"
    client_id = convert_resp.json().get("clientId")
    assert client_id, "Client ID not returned in convert response"
    
    # 7. Create quote
    quote_payload = {
        "carId": car_id,
        "clientId": None,
        "leadId": lead_id,
        "proposedPrice": 15000.0,
        "paymentMethod": 0, # Contado
        "validUntil": "2026-07-20T00:00:00Z",
        "comments": "Quote comments"
    }
    quote_resp = session.post(QUOTES_URL, headers=headers, json=quote_payload, timeout=TIMEOUT)
    assert quote_resp.status_code == 201, f"Create quote failed: {quote_resp.text}"
    quote_id = quote_resp.json().get("id")
    assert quote_id, "Quote ID not found in create quote response"
    
    # 8. Accept quote
    accept_resp = session.post(f"{QUOTES_URL}/{quote_id}/accept", headers=headers, timeout=TIMEOUT)
    assert accept_resp.status_code == 200, f"Accept quote failed: {accept_resp.text}"
    
    # 9. Verify quote status changed to Accepted
    get_quote_resp = session.get(f"{QUOTES_URL}/{quote_id}", headers=headers, timeout=TIMEOUT)
    assert get_quote_resp.status_code == 200, f"Get quote failed: {get_quote_resp.text}"
    quote_info = get_quote_resp.json()
    assert quote_info.get("status") == "Accepted", f"Quote status expected 'Accepted', got '{quote_info.get('status')}'"
    
    # 10. Verify related domain events (lead status advanced, client created)
    # Since domain events are processed asynchronously via an Outbox pattern background job,
    # we retry querying the lead status for up to 25 seconds.
    lead_status = None
    for _ in range(25):
        get_lead_resp = session.get(f"{LEADS_URL}/{lead_id}", headers=headers, timeout=TIMEOUT)
        assert get_lead_resp.status_code == 200, f"Get lead failed: {get_lead_resp.text}"
        lead_info = get_lead_resp.json()
        lead_status = lead_info.get("status")
        if lead_status in (4, "Ganado"):
            break
        time.sleep(1)
        
    assert lead_status in (4, "Ganado"), f"Lead status expected Ganado (4), got {lead_status}"
    
    get_client_resp = session.get(f"{CLIENTS_URL}/{client_id}", headers=headers, timeout=TIMEOUT)
    assert get_client_resp.status_code == 200, f"Get client failed: {get_client_resp.text}"
    client_info = get_client_resp.json()
    assert client_info.get("id") == client_id, "Client ID mismatch"
    
    # Cleanup
    try:
        session.delete(f"{QUOTES_URL}/{quote_id}", headers=headers, timeout=TIMEOUT)
    except Exception:
        pass
    try:
        session.delete(f"{CLIENTS_URL}/{client_id}", headers=headers, timeout=TIMEOUT)
    except Exception:
        pass
    try:
        session.delete(f"{LEADS_URL}/{lead_id}", headers=headers, timeout=TIMEOUT)
    except Exception:
        pass

if __name__ == '__main__':
    test_acceptquoteandtriggerdomainevents()
    print("TC006 PASSED")
