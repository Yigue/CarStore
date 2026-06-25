import requests
import time

BASE_URL = "http://localhost:8080"
LOGIN_URL = f"{BASE_URL}/api/v1/users/login"
CARS_URL = f"{BASE_URL}/api/v1/cars"
LEADS_URL = f"{BASE_URL}/api/v1/leads"


def test_link_vehicle_to_lead():
    session = requests.Session()
    try:
        # 1. Authenticate and get token
        login_resp = session.post(
            LOGIN_URL,
            json={"email": "admin@carstore.com", "password": "Admin123!"},
            timeout=30,
        )
        assert login_resp.status_code == 200, "Login failed"
        token = login_resp.json().get("token")
        assert token, "Token missing in login response"
        headers = {"Authorization": f"Bearer {token}"}

        # 2. Fetch first car ID dynamically
        cars_resp = session.get(CARS_URL, headers=headers, timeout=30)
        assert cars_resp.status_code == 200, "Failed to fetch cars"
        cars_data = cars_resp.json()
        items = cars_data.get("items")
        assert items and isinstance(items, list), "No cars found"
        car_id = items[0].get("id")
        assert car_id, "Car ID missing"

        # 3. Create a new lead (since no existing lead ID)
        lead_payload = {
            "clientName": "John Doe",
            "email": "johndoe@example.com",
            "phone": "1234567890",
            "source": 0,
            "notes": "Test notes",
            "interestedVehicleId": None,
        }
        create_lead_resp = session.post(LEADS_URL, json=lead_payload, headers=headers, timeout=30)
        assert create_lead_resp.status_code == 201, "Lead creation failed"
        lead_id = create_lead_resp.json().get("id")
        assert lead_id, "Lead ID missing in creation response"

        # 4. Link vehicle to the lead using PATCH /api/v1/leads/{id}/vehicle
        link_vehicle_url = f"{LEADS_URL}/{lead_id}/vehicle"
        patch_payload = {"vehicleId": car_id}
        patch_resp = session.patch(link_vehicle_url, json=patch_payload, headers=headers, timeout=30)
        assert patch_resp.status_code == 204, f"Linking vehicle failed with status {patch_resp.status_code}"

        # 5. Verify the vehicle is associated with the lead by GET /api/v1/leads/{id}
        get_lead_url = f"{LEADS_URL}/{lead_id}"
        lead_resp = session.get(get_lead_url, headers=headers, timeout=30)
        assert lead_resp.status_code == 200, "Failed to fetch lead after linking vehicle"
        lead_data = lead_resp.json()
        linked_vehicle_id = lead_data.get("interestedVehicleId") or lead_data.get("vehicleId")
        # The PRD doesn't explicitly show response shape but expects vehicle linked verification
        # We'll accept both keys in case of naming differences and validate that vehicleId matches.
        assert linked_vehicle_id == car_id, "Vehicle not linked to lead correctly"

    finally:
        # Clean up: delete the lead created
        if 'lead_id' in locals():
            del_url = f"{LEADS_URL}/{lead_id}"
            try:
                session.delete(del_url, headers=headers, timeout=30)
            except Exception:
                pass


# test_link_vehicle_to_lead()